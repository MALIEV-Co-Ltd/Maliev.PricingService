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

    public PricingOrchestrator(
        IPricingDbContext context,
        IPricingEngine ruleEngine,
        IJobServiceClient jobServiceClient,
        ILogger<PricingOrchestrator> logger,
        IPublishEndpoint publishEndpoint)
    {
        _context = context;
        _ruleEngine = ruleEngine;
        _jobServiceClient = jobServiceClient;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
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

        var ruleResult = await _ruleEngine.CalculateAsync(request, config, cancellationToken);

        // Apply lead time multiplier (e.g. Economy < 1.0, Standard = 1.0, Express > 1.0).
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

        var adjustedUnitPrice = ruleResult.UnitPrice * leadTimeMultiplier;
        var adjustedTotal = adjustedUnitPrice * request.Quantity;

        var processCode = NormalizeProcessCode(request.ManufacturingProcessName);
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
                {
                    queueDepth = depth;
                }
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
            // No capacity config found — use conservative process-type defaults so that
            // large quantities still produce proportionally longer estimates.
            _logger.LogWarning(
                "No MachineCapacityConfig found for process '{ProcessName}'. Using default throughput fallback.",
                request.ManufacturingProcessName);

            var defaultPartsPerDay = GetDefaultThroughput(request.ManufacturingProcessName);
            var productionDays = (int)Math.Ceiling((double)request.Quantity / defaultPartsPerDay);
            estimatedLeadTimeDays = Math.Max(productionDays + 2, 5); // +2 setup/shipping buffer, minimum 5
        }

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
            TotalUnitPrice = adjustedUnitPrice,
            TotalPrice = adjustedTotal,
            ConfidenceLevel = ruleResult.ConfidenceScore,
            CurrencyCode = request.Currency,
            ValidFrom = now,
            ValidUntil = now.AddDays(30),
            CalculatedAt = now,
            CalculationDuration = TimeSpan.FromMilliseconds(100),
            CorrelationId = request.CorrelationId?.ToString()
        };

        _context.AuditRecords.Add(auditRecord);

        _context.Snapshots.Add(new PricingSnapshot
        {
            Id = Guid.NewGuid(),
            OrderId = "TEMP",
            EmployeeId = "SYSTEM",
            MaterialCode = request.MaterialCode,
            CalculatedPrice = adjustedTotal,
            PricingAuditRecordId = auditRecord.Id,
            CreatedAt = now
        });

        await _context.SaveChangesAsync(cancellationToken);

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
                        MaterialCost: 0,
                        SupportCost: 0,
                        MachineTimeCost: 0,
                        SetupCost: 0,
                        ComplexitySurcharge: 0,
                        SubtotalBeforeMargin: 0,
                        MarginAmount: 0,
                        TotalPrice: (double)adjustedTotal
                    ),
                    TotalUnitPrice: (double)adjustedUnitPrice,
                    TotalPrice: (double)adjustedTotal,
                    Currency: request.Currency,
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

        return ruleResult with
        {
            UnitPrice = adjustedUnitPrice,
            TotalAmount = adjustedTotal,
            AuditId = auditRecord.Id,
            EstimatedLeadTimeDays = estimatedLeadTimeDays
        };
    }

    public async Task<PricingResult> AuditCalculationAsync(Guid calculationId, PricingResult result, CancellationToken cancellationToken = default)
    {
        _context.AuditRecords.Add(new PricingAuditRecord
        {
            Id = Guid.NewGuid(),
            FileId = Guid.Empty,
            CustomerId = Guid.Empty,
            TotalUnitPrice = result.UnitPrice,
            TotalPrice = result.TotalAmount,
            CalculatedAt = DateTime.UtcNow,
            CalculationDuration = TimeSpan.Zero,
            Strategy = PricingStrategy.Manual
        });

        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// Returns a conservative default parts-per-day throughput for the given process
    /// when no MachineCapacityConfig row exists in the database.
    /// </summary>
    private static double GetDefaultThroughput(string processName)
    {
        var name = processName.ToUpperInvariant();
        if (name.Contains("FDM") || name.Contains("FFF")) return 8.0;
        if (name.Contains("SLA") || name.Contains("MSLA") || name.Contains("DLP")) return 4.0;
        if (name.Contains("SLS") || name.Contains("MJF")) return 20.0;
        if (name.Contains("CNC")) return 3.0;
        return 5.0; // generic fallback
    }

    /// <summary>
    /// Normalizes a display name (e.g. "3D Printing (FDM)") to the canonical process code
    /// used in MachineCapacityConfig (e.g. "FDM").
    /// </summary>
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
        if (name.Contains("DMLS") || name.Contains("DMLS")) return "DMLS";
        return processName;
    }
}
