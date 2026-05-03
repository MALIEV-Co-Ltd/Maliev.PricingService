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
        if (request.MaterialId == Guid.Empty)
        {
            _logger.LogDebug(
                "Pricing request has MaterialId == Guid.Empty; skipping calculation. FileId: {FileId}",
                request.FileId);
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

        var config = await _context.Configurations
            .Where(c => c.MaterialId == request.MaterialId
                     && c.ManufacturingProcessId == request.ManufacturingProcessId
                     && c.IsActive
                     && c.EffectiveFrom <= DateTime.UtcNow
                     && (c.EffectiveTo == null || c.EffectiveTo >= DateTime.UtcNow))
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

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
        // Step 1: breakdown.SubtotalBeforeMargin (from engine)
        // Step 2: apply margin
        decimal marginedUnitPrice = breakdown.SubtotalBeforeMargin * config.MarginMultiplier;
        decimal marginAmount = marginedUnitPrice - breakdown.SubtotalBeforeMargin;

        // Step 3: volume discount (applied to margined list price, before surcharges)
        var (volumeTierId, volumeDiscountPct) = await _discountResolver.ResolveAsync((int)request.Quantity, cancellationToken);
        decimal volumeDiscountAmount = marginedUnitPrice * volumeDiscountPct / 100m;
        decimal discountedUnitPrice = marginedUnitPrice - volumeDiscountAmount;

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

        decimal surchargedUnitPrice = discountedUnitPrice * leadTimeMultiplier * toleranceMultiplier;

        // Step 5: apply minimum order price floor (in THB)
        decimal flooredUnitPriceThb = Math.Max(surchargedUnitPrice, breakdown.MinimumOrderPriceFloor);

        // Step 6: total in THB
        decimal totalThb = flooredUnitPriceThb * request.Quantity;

        // Step 7: convert to customer currency (snapshot rate on audit; fallback = 1.0 if service unavailable)
        decimal exchangeRate = 1.0m;
        var currency = request.Currency ?? "THB";
        if (!string.Equals(currency, "THB", StringComparison.OrdinalIgnoreCase))
        {
            exchangeRate = await _currencyClient.GetExchangeRateAsync("THB", currency, cancellationToken);
            _logger.LogDebug("Applied FX rate {Rate} for THB→{Currency}", exchangeRate, currency);
        }

        decimal flooredUnitPrice = flooredUnitPriceThb * exchangeRate;
        decimal total = totalThb * exchangeRate;

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
        try
        {
            await _publishEndpoint.Publish(new PriceCalculatedEvent(
                MessageId: Guid.NewGuid(),
                MessageName: "PriceCalculatedEvent",
                MessageType: MessageType.Event,
                MessageVersion: "1.0.0",
                PublishedBy: "PricingService",
                ConsumedBy: ["IntranetBff", "QuotationService"],
                CorrelationId: request.CorrelationId ?? Guid.NewGuid(),
                CausationId: null,
                OccurredAtUtc: DateTimeOffset.UtcNow,
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
                    CalculatedAt: DateTimeOffset.UtcNow,
                    StoragePath: request.StoragePath,
                    EstimatedLeadTimeDays: estimatedLeadTimeDays
                )
            ), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to publish PriceCalculatedEvent for AuditId={AuditId}; result will still be returned",
                auditRecord.Id);
        }

        return new PricingResult
        {
            UnitPrice = flooredUnitPrice,
            TotalAmount = total,
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
}
