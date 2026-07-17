using System.Net;
using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.MessagingContracts.Contracts.Pricing;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Domain.Entities;
using Maliev.PricingService.Domain.Enums;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.PricingService.Application.Services;

public class PricingOrchestrator : IPricingOrchestrator
{
    private static readonly TimeSpan PriceCalculatedEventPublishTimeout = TimeSpan.FromSeconds(5);

    private readonly IPricingDbContext _context;
    private readonly IPricingEngine _ruleEngine;
    private readonly IJobServiceClient _jobServiceClient;
    private readonly ILogger<PricingOrchestrator> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IVolumeDiscountResolver _discountResolver;
    private readonly ICurrencyServiceClient _currencyClient;

    public PricingOrchestrator(
        IPricingDbContext context,
        IPricingEngine ruleEngine,
        IJobServiceClient jobServiceClient,
        ILogger<PricingOrchestrator> logger,
        IPublishEndpoint publishEndpoint,
        IVolumeDiscountResolver discountResolver,
        ICurrencyServiceClient currencyClient)
    {
        _context = context;
        _ruleEngine = ruleEngine;
        _jobServiceClient = jobServiceClient;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
        _discountResolver = discountResolver;
        _currencyClient = currencyClient;
    }

    public async Task<PricingResult> CalculatePriceAsync(PricingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity < 1m)
        {
            _logger.LogWarning(
                "Pricing request has invalid quantity {Quantity}; returning no price for FileId {FileId}",
                request.Quantity,
                request.FileId);
            return new PricingResult
            {
                UnitPrice = 0m,
                TotalAmount = 0m,
                ConfidenceScore = 1m,
                EngineName = "None",
                AuditId = Guid.Empty,
                EstimatedLeadTimeDays = 0
            };
        }

        var effectiveAt = DateTime.UtcNow;
        var config = await _context.Configurations
            .Where(c => c.MaterialId == request.MaterialId
                     && c.ManufacturingProcessId == request.ManufacturingProcessId
                     && c.IsActive
                     && c.EffectiveFrom <= effectiveAt
                     && (c.EffectiveTo == null || c.EffectiveTo >= effectiveAt))
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (config == null)
        {
            var materialCode = NormalizeLookupCode(request.MaterialCode);
            var fallbackProcessCode = NormalizeConfigurationProcessCode(request.ManufacturingProcessName);

            if (materialCode.Length > 0 && fallbackProcessCode.Length > 0)
            {
                var codeMatches = await _context.Configurations
                    .Where(c => c.MaterialCode == materialCode
                             && c.ManufacturingProcessCode == fallbackProcessCode
                             && c.IsActive
                             && c.EffectiveFrom <= effectiveAt
                             && (c.EffectiveTo == null || c.EffectiveTo >= effectiveAt))
                    .Take(2)
                    .ToListAsync(cancellationToken);

                if (codeMatches.Count == 1)
                {
                    config = codeMatches[0];
                    _logger.LogInformation(
                        "Pricing configuration IDs drifted; using configuration {PricingConfigurationId} matched by stable codes {MaterialCode}/{ManufacturingProcessCode}",
                        config.Id,
                        materialCode,
                        fallbackProcessCode);
                }
                else if (codeMatches.Count > 1)
                {
                    _logger.LogWarning(
                        "Ambiguous active pricing configurations found for stable codes {MaterialCode}/{ManufacturingProcessCode}; refusing fallback",
                        materialCode,
                        fallbackProcessCode);
                }
            }
        }

        if (config == null)
        {
            _logger.LogWarning(
                "No active pricing configuration found for MaterialId: {MaterialId}, ManufacturingProcessId: {ManufacturingProcessId}",
                request.MaterialId,
                request.ManufacturingProcessId);

            return new PricingResult
            {
                UnitPrice = 0,
                TotalAmount = 0,
                ConfidenceScore = 1.0m,
                EngineName = "None",
                AuditId = Guid.Empty,
                EstimatedLeadTimeDays = 0
            };
        }

        // ── China Outsourcing pre-processing ────────────────────────────────────
        // Convert vendor quote to THB and inject as VendorQuoteThb so the calculator receives it.
        var processCode = NormalizeProcessCode(request.ManufacturingProcessName);
        PricingRequest effectiveRequest = request;
        if (processCode == "CHINA_OS" && request.VendorQuoteAmount is > 0m)
        {
            var vendorCurrency = request.VendorQuoteCurrency ?? "THB";
            decimal vendorQuoteThb = string.Equals(vendorCurrency, "THB", StringComparison.OrdinalIgnoreCase)
                ? request.VendorQuoteAmount.Value
                : request.VendorQuoteAmount.Value * await _currencyClient.GetExchangeRateAsync(vendorCurrency, "THB", cancellationToken);

            // Inject VendorQuoteThb via a synthetic request (immutable record, so create new)
            effectiveRequest = request with { VendorQuoteAmount = vendorQuoteThb, VendorQuoteCurrency = "THB" };
            _logger.LogDebug(
                "CHINA_OS: converted vendor quote {Amount} {Currency} → {ThbAmount} THB",
                request.VendorQuoteAmount, vendorCurrency, vendorQuoteThb);
        }

        var engineResult = await _ruleEngine.CalculateAsync(effectiveRequest, config, cancellationToken);
        var breakdown = engineResult.Breakdown;

        // ── Canonical Composition Order ──────────────────────────────────────────
        // Step 1: split the engine breakdown into variable per-unit cost and the
        // fixed setup/tooling cost for the job. Calculators deliberately include
        // SetupCost and its setup-derived DFM surcharge in SubtotalBeforeMargin
        // so the breakdown remains additive, but neither may be multiplied by quantity.
        decimal fixedLineCost = Math.Max(0m, breakdown.SetupCost + breakdown.FixedDfmSurcharge);
        decimal variableUnitCost = Math.Max(0m, breakdown.SubtotalBeforeMargin - fixedLineCost);

        // Step 2: apply margin to both cost classes. Fixed setup receives margin
        // once for the completed line, while variable cost receives margin per unit.
        decimal marginedVariableUnitPrice = variableUnitCost * config.MarginMultiplier;
        decimal marginedFixedSetup = fixedLineCost * config.MarginMultiplier;
        decimal marginAmount =
            (marginedVariableUnitPrice - variableUnitCost) +
            ((marginedFixedSetup - fixedLineCost) / request.Quantity);

        // Step 3: volume discount applies only to variable production cost. Setup
        // and tooling are one-time job costs and are not diluted by a volume tier.
        var (volumeTierId, volumeDiscountPct) = await _discountResolver.ResolveAsync((int)request.Quantity, cancellationToken);
        decimal volumeDiscountAmount = marginedVariableUnitPrice * volumeDiscountPct / 100m;
        decimal discountedVariableUnitPrice = marginedVariableUnitPrice - volumeDiscountAmount;

        // Step 4: apply lead-time and tolerance surcharges (on top of discounted list price)
        decimal leadTimeMultiplier = 1.0m;
        if (!string.IsNullOrEmpty(request.LeadTimeCode))
        {
            var ltOption = await _context.LeadTimeOptions
                .AsNoTracking()
                .FirstOrDefaultAsync(lt => lt.Code == request.LeadTimeCode, cancellationToken);
            if (ltOption is not null)
            {
                leadTimeMultiplier = ltOption.PriceMultiplier;
                _logger.LogDebug(
                    "Applied lead time multiplier {Multiplier} for code {Code}",
                    leadTimeMultiplier, request.LeadTimeCode);
            }
        }

        decimal toleranceMultiplier = 1.0m;
        if (request.ToleranceAdditionalCostPercent is > 0m)
        {
            toleranceMultiplier = 1m + request.ToleranceAdditionalCostPercent.Value / 100m;
            _logger.LogDebug(
                "Applied tolerance multiplier {Multiplier} for code {Code}",
                toleranceMultiplier, request.ToleranceCode);
        }

        // Lead-time and tolerance multipliers price job urgency and inspection
        // effort, so they apply to the completed variable and fixed portions. The
        // fixed portion still appears exactly once in the line total.
        decimal surchargeMultiplier = leadTimeMultiplier * toleranceMultiplier;
        decimal surchargedVariableUnitPrice = discountedVariableUnitPrice * surchargeMultiplier;
        decimal surchargedFixedSetup = marginedFixedSetup * surchargeMultiplier;

        // Step 5: apply minimum order price floor to the full line (in THB)
        decimal rawTotalThb = surchargedVariableUnitPrice * request.Quantity + surchargedFixedSetup;
        decimal flooredTotalThb = Math.Max(rawTotalThb, breakdown.MinimumOrderPriceFloor);
        decimal flooredUnitPriceThb = flooredTotalThb / request.Quantity;

        // Step 6: convert to customer currency. A missing downstream rate fails
        // the calculation closed rather than substituting commercial parity.
        decimal exchangeRate = 1.0m;
        var currency = request.Currency ?? "THB";
        if (!string.Equals(currency, "THB", StringComparison.OrdinalIgnoreCase))
        {
            exchangeRate = await _currencyClient.GetExchangeRateAsync("THB", currency, cancellationToken);
            _logger.LogDebug("Applied FX rate {Rate} for THB→{Currency}", exchangeRate, currency);
        }

        decimal flooredUnitPrice = flooredUnitPriceThb * exchangeRate;
        decimal unitPriceBeforeVolumeDiscount =
            ((marginedVariableUnitPrice * request.Quantity + marginedFixedSetup) * surchargeMultiplier /
             request.Quantity) * exchangeRate;
        decimal volumeDiscountUnitAmount = Math.Max(0m, unitPriceBeforeVolumeDiscount - flooredUnitPrice);
        decimal total = flooredTotalThb * exchangeRate;

        // ── Lead-Time Estimation ─────────────────────────────────────────────────
        var capacity = await _context.MachineCapacityConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ProcessType == processCode && m.IsActive, cancellationToken);

        int estimatedLeadTimeDays;
        if (capacity is { AvgThroughputPartsPerDay: > 0, MachineCount: > 0 })
        {
            var partsPerDay = capacity.AvgThroughputPartsPerDay * capacity.MachineCount;
            var productionDays = Math.Ceiling((double)request.Quantity / (double)partsPerDay);

            var queueDepth = 0;
            try
            {
                var queueDepths = await _jobServiceClient.GetQueueDepthByTechnologyAsync(
                    request.ManufacturingProcessName, cancellationToken);
                if (queueDepths.TryGetValue(request.ManufacturingProcessName, out var depth))
                    queueDepth = depth;
            }
            catch (Exception ex) when (ex is OperationCanceledException or
                                       ServiceTokenExchangeException or
                                       HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden })
            {
                _logger.LogWarning(
                    ex,
                    "JobService request could not be trusted or was canceled for technology {Technology}",
                    request.ManufacturingProcessName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to get queue depth from JobService for technology {Technology}. Using 0.",
                    request.ManufacturingProcessName);
            }

            var queueDays = Math.Ceiling((double)queueDepth / (double)capacity.MachineCount);
            estimatedLeadTimeDays = (int)(productionDays + queueDays)
                                    + capacity.SetupTimeDays
                                    + capacity.ShippingBufferDays;
        }
        else
        {
            _logger.LogWarning(
                "No MachineCapacityConfig found for process '{ProcessName}'. Using default throughput fallback.",
                request.ManufacturingProcessName);

            var defaultPartsPerDay = GetDefaultThroughput(request.ManufacturingProcessName);
            var productionDays = (int)Math.Ceiling((double)request.Quantity / defaultPartsPerDay);
            estimatedLeadTimeDays = Math.Max(productionDays + 2, 5);
        }

        // ── Persist Audit Record ─────────────────────────────────────────────────
        var now = DateTime.UtcNow;
        var auditRecord = new PricingAuditRecord
        {
            Id = Guid.NewGuid(),
            FileId = request.FileId,
            CustomerId = request.CustomerId,
            MaterialId = request.MaterialId,
            ManufacturingProcessId = request.ManufacturingProcessId,
            MaterialCode = request.MaterialCode,
            ManufacturingProcessName = request.ManufacturingProcessName,
            Quantity = (int)request.Quantity,
            InputVolumeCm3 = request.Geometry.VolumeCm3,
            InputSupportVolumeCm3 = request.Geometry.SupportVolumeCm3,
            InputSurfaceAreaCm2 = request.Geometry.SurfaceAreaCm2,
            InputBoundingBoxX = request.Geometry.BoundingBoxX,
            InputBoundingBoxY = request.Geometry.BoundingBoxY,
            InputBoundingBoxZ = request.Geometry.BoundingBoxZ,
            InputIsManifold = request.Geometry.IsManifold,
            InputTriangleCount = request.Geometry.TriangleCount,
            PricingConfigurationId = config.Id,
            ConfigMaterialPricePerCm3 = config.MaterialPricePerCm3,
            ConfigSupportPricePerCm3 = config.SupportMaterialPricePerCm3,
            ConfigMachineHourlyRate = config.MachineHourlyRate,
            ConfigMarginMultiplier = config.MarginMultiplier,
            Strategy = PricingStrategy.RuleBased,
            MLModelVersion = null,
            // Breakdown — fully populated
            MaterialCost = breakdown.MaterialCost,
            SupportMaterialCost = breakdown.SupportMaterialCost,
            MachineTimeCost = breakdown.MachineTimeCost,
            SetupCost = breakdown.SetupCost,
            FixedDfmSurcharge = breakdown.FixedDfmSurcharge,
            ComplexitySurcharge = breakdown.ComplexitySurcharge,
            SubtotalBeforeMargin = breakdown.SubtotalBeforeMargin,
            MarginAmount = marginAmount,
            VolumeDiscountTierId = volumeTierId,
            VolumeDiscountPercent = volumeDiscountPct,
            VolumeDiscountAmount = volumeDiscountAmount,
            ExchangeRate = exchangeRate,
            TotalUnitPrice = flooredUnitPrice,
            TotalPrice = total,
            ConfidenceLevel = 1.0m,
            CurrencyCode = currency,
            ValidFrom = now,
            ValidUntil = now.AddDays(30),
            CalculatedAt = now,
            CalculationDuration = TimeSpan.FromMilliseconds(100),
            CorrelationId = request.CorrelationId?.ToString()
        };

        _context.AuditRecords.Add(auditRecord);

        // Snapshot stub: OrderId and EmployeeId are populated by a SnapshotFinaliser
        // when the quotation is accepted (Phase 6 lifecycle integration).
        _context.Snapshots.Add(new PricingSnapshot
        {
            Id = Guid.NewGuid(),
            OrderId = string.Empty,
            EmployeeId = string.Empty,
            MaterialCode = request.MaterialCode,
            CalculatedPrice = total,
            PricingAuditRecordId = auditRecord.Id,
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

        // ── Publish Event ────────────────────────────────────────────────────────
        var eventCorrelationId = request.CorrelationId ?? Guid.NewGuid();
        var eventTimestamp = DateTimeOffset.UtcNow;
        try
        {
            using var publishTimeout = new CancellationTokenSource(PriceCalculatedEventPublishTimeout);
            await _publishEndpoint.Publish(new PriceCalculatedEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "PriceCalculatedEvent",
                MessageType: MessageType.Event,
                MessageVersion: "1.0.0",
                PublishedBy: "PricingService",
                ConsumedBy: ["IntranetBff", "QuotationService"],
                CorrelationId: eventCorrelationId,
                CausationId: null,
                OccurredAtUtc: eventTimestamp,
                IsPublic: false,
                Payload: new PriceCalculatedEventPayload(
                    PricingAuditId: auditRecord.Id,
                    QuotationId: null,
                    FileId: request.FileId,
                    CustomerId: request.CustomerId,
                    MaterialId: request.MaterialId,
                    ProcessId: request.ManufacturingProcessId,
                    Quantity: (int)request.Quantity,
                    InputVolumeCm3: (double)request.Geometry.VolumeCm3,
                    InputSupportVolumeCm3: (double)request.Geometry.SupportVolumeCm3,
                    InputSurfaceAreaCm2: (double)request.Geometry.SurfaceAreaCm2,
                    Strategy: auditRecord.Strategy.ToString(),
                    MlModelVersion: auditRecord.MLModelVersion,
                    ConfidenceLevel: (double)auditRecord.ConfidenceLevel,
                    PricingConfigurationId: auditRecord.PricingConfigurationId,
                    Breakdown: new PriceCalculatedEventPayloadBreakdown(
                        MaterialCost: (double)breakdown.MaterialCost,
                        SupportCost: (double)breakdown.SupportMaterialCost,
                        MachineTimeCost: (double)breakdown.MachineTimeCost,
                        SetupCost: (double)breakdown.SetupCost,
                        ComplexitySurcharge: (double)breakdown.ComplexitySurcharge,
                        SubtotalBeforeMargin: (double)breakdown.SubtotalBeforeMargin,
                        MarginAmount: (double)marginAmount,
                        TotalPrice: (double)total
                    ),
                    TotalUnitPrice: (double)flooredUnitPrice,
                    TotalPrice: (double)total,
                    Currency: currency,
                    ValidUntil: new DateTimeOffset(auditRecord.ValidUntil, TimeSpan.Zero),
                    CalculatedAt: eventTimestamp,
                    StoragePath: request.StoragePath,
                    EstimatedLeadTimeDays: estimatedLeadTimeDays
                )
            ), publishTimeout.Token);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex,
                "PriceCalculatedEvent publish was canceled or timed out for AuditId={AuditId}; result will still be returned",
                auditRecord.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to publish PriceCalculatedEvent for AuditId={AuditId}; result will still be returned",
                auditRecord.Id);
        }

        // Publish v1 first so existing consumers keep receiving the established contract.
        // The additive v2 confirmation is isolated so either broker publication can fail independently.
        try
        {
            using var publishTimeout = new CancellationTokenSource(PriceCalculatedEventPublishTimeout);
            await _publishEndpoint.Publish(new PriceCalculatedEventV2(
                MessageId: Guid.NewGuid(),
                MessageName: "PriceCalculatedEventV2",
                MessageType: MessageType.Event,
                MessageVersion: "2.0.0",
                PublishedBy: "PricingService",
                ConsumedBy: [],
                CorrelationId: eventCorrelationId,
                CausationId: null,
                OccurredAtUtc: eventTimestamp,
                IsPublic: false,
                Payload: new PriceCalculatedEventV2Payload(
                    PricingAuditId: auditRecord.Id,
                    QuotationId: null,
                    FileId: request.FileId,
                    CustomerId: request.CustomerId,
                    MaterialId: request.MaterialId,
                    ProcessId: request.ManufacturingProcessId,
                    Quantity: (int)request.Quantity,
                    InputVolumeCm3: (double)request.Geometry.VolumeCm3,
                    InputSupportVolumeCm3: (double)request.Geometry.SupportVolumeCm3,
                    InputSurfaceAreaCm2: (double)request.Geometry.SurfaceAreaCm2,
                    Strategy: auditRecord.Strategy.ToString(),
                    MlModelVersion: auditRecord.MLModelVersion,
                    ConfidenceLevel: (double)auditRecord.ConfidenceLevel,
                    PricingConfigurationId: auditRecord.PricingConfigurationId,
                    Breakdown: new PriceCalculatedEventV2PayloadBreakdown(
                        MaterialCost: (double)breakdown.MaterialCost,
                        SupportCost: (double)breakdown.SupportMaterialCost,
                        MachineTimeCost: (double)breakdown.MachineTimeCost,
                        SetupCost: (double)breakdown.SetupCost,
                        FixedDfmSurcharge: (double)breakdown.FixedDfmSurcharge,
                        ComplexitySurcharge: (double)breakdown.ComplexitySurcharge,
                        SubtotalBeforeMargin: (double)breakdown.SubtotalBeforeMargin,
                        MarginAmount: (double)marginAmount,
                        TotalPrice: (double)total
                    ),
                    TotalUnitPrice: (double)flooredUnitPrice,
                    TotalPrice: (double)total,
                    Currency: currency,
                    ValidUntil: new DateTimeOffset(auditRecord.ValidUntil, TimeSpan.Zero),
                    CalculatedAt: eventTimestamp,
                    StoragePath: request.StoragePath,
                    EstimatedLeadTimeDays: estimatedLeadTimeDays
                )
            ), publishTimeout.Token);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogDebug(ex,
                "PriceCalculatedEventV2 publish was canceled or timed out for AuditId={AuditId}; result will still be returned",
                auditRecord.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to publish PriceCalculatedEventV2 for AuditId={AuditId}; result will still be returned",
                auditRecord.Id);
        }

        return new PricingResult
        {
            UnitPrice = flooredUnitPrice,
            TotalAmount = total,
            UnitPriceBeforeVolumeDiscount = unitPriceBeforeVolumeDiscount,
            VolumeDiscountUnitAmount = volumeDiscountUnitAmount,
            VolumeDiscountPercent = volumeDiscountPct,
            ConfidenceScore = 1.0m,
            EngineName = engineResult.EngineName,
            AuditId = auditRecord.Id,
            EstimatedLeadTimeDays = estimatedLeadTimeDays
        };
    }

    public async Task<PricingResult> AuditCalculationAsync(Guid calculationId, PricingResult result, CancellationToken cancellationToken = default)
    {
        // Carry forward required fields from the original calculation so the audit record is valid.
        var original = await _context.AuditRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == calculationId, cancellationToken);

        var now = DateTime.UtcNow;
        _context.AuditRecords.Add(new PricingAuditRecord
        {
            Id = Guid.NewGuid(),
            FileId = original?.FileId ?? Guid.Empty,
            CustomerId = original?.CustomerId ?? Guid.Empty,
            MaterialId = original?.MaterialId ?? Guid.Empty,
            ManufacturingProcessId = original?.ManufacturingProcessId ?? Guid.Empty,
            MaterialCode = original?.MaterialCode ?? string.Empty,
            ManufacturingProcessName = original?.ManufacturingProcessName ?? string.Empty,
            Quantity = original?.Quantity ?? 1,
            PricingConfigurationId = original?.PricingConfigurationId ?? Guid.Empty,
            TotalUnitPrice = result.UnitPrice,
            TotalPrice = result.TotalAmount,
            ConfidenceLevel = result.ConfidenceScore,
            CurrencyCode = original?.CurrencyCode ?? "THB",
            ExchangeRate = 1.0m,
            ValidFrom = now,
            ValidUntil = now.AddDays(30),
            CalculatedAt = now,
            CalculationDuration = TimeSpan.Zero,
            Strategy = PricingStrategy.Manual
        });

        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static double GetDefaultThroughput(string processName)
    {
        var name = processName.ToUpperInvariant();
        if (name.Contains("FDM") || name.Contains("FFF")) return 8.0;
        if (name.Contains("SLA") || name.Contains("MSLA") || name.Contains("DLP")) return 4.0;
        if (name.Contains("SLS") || name.Contains("MJF")) return 20.0;
        if (name.Contains("CNC")) return 3.0;
        return 5.0;
    }

    private static string NormalizeProcessCode(string processName)
    {
        var name = processName.ToUpperInvariant();
        if (name.Contains("FDM") || name.Contains("FFF")) return "FDM";
        if (name.Contains("SLA") || name.Contains("MSLA") || name.Contains("DLP")) return "SLA";
        if (name.Contains("CNC_MILL") || name.Contains("CNC MILL")) return "CNC_MILL";
        if (name.Contains("CNC_TURN") || name.Contains("CNC TURN")) return "CNC_TURN";
        if (name.Contains("CNC")) return "CNC";
        if (name.Contains("SLS")) return "SLS";
        if (name.Contains("MJF")) return "MJF";
        if (name == "MJ" || name.Contains("MATERIAL JETTING")) return "MJ";
        if (name.Contains("BJ") || name.Contains("BINDER JETTING")) return "BJ";
        if (name.Contains("DMLS")) return "DMLS";
        return processName;
    }

    private static string NormalizeLookupCode(string? code)
    {
        return code?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    private static string NormalizeConfigurationProcessCode(string? processName)
    {
        var name = NormalizeLookupCode(processName);
        if (name.Contains("CNC_MILL", StringComparison.Ordinal)
            || name.Contains("CNC MILL", StringComparison.Ordinal)) return "CNC_MILL";
        if (name.Contains("CNC_TURN", StringComparison.Ordinal)
            || name.Contains("CNC TURN", StringComparison.Ordinal)) return "CNC_TURN";
        if (name.Contains("FDM", StringComparison.Ordinal)
            || name.Contains("FFF", StringComparison.Ordinal)) return "FDM";
        if (name.Contains("SLA", StringComparison.Ordinal)
            || name.Contains("MSLA", StringComparison.Ordinal)
            || name.Contains("DLP", StringComparison.Ordinal)) return "SLA_DLP";
        if (name.Contains("CNC", StringComparison.Ordinal)) return "CNC";
        if (name.Contains("SLS", StringComparison.Ordinal)) return "SLS";
        if (name.Contains("MJF", StringComparison.Ordinal)) return "MJF";
        if (name == "MJ" || name.Contains("MATERIAL JETTING", StringComparison.Ordinal)) return "MJ";
        if (name == "BJ" || name.Contains("BINDER JETTING", StringComparison.Ordinal)) return "BJ";
        if (name.Contains("DMLS", StringComparison.Ordinal)) return "DMLS";
        return name.Replace(' ', '_').Replace('/', '_').Replace('-', '_');
    }
}
