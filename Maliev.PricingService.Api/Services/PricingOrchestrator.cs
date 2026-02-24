using Maliev.MessagingContracts.Contracts.Pricing;
using Maliev.MessagingContracts.Generated;
using Maliev.PricingService.Api.Interfaces;
using Maliev.PricingService.Data;
using Maliev.PricingService.Data.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using EntityPricingStrategy = Maliev.PricingService.Data.Entities.PricingStrategy;

namespace Maliev.PricingService.Api.Services;

/// <summary>
/// Orchestrates the pricing calculation process.
/// </summary>
public class PricingOrchestrator : IPricingOrchestrator
{
    private readonly PricingDbContext _dbContext;
    private readonly IPricingEngine _pricingEngine;
    private readonly IMemoryCache _cache;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PricingOrchestrator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PricingOrchestrator"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="pricingEngine">The pricing engine.</param>
    /// <param name="cache">The memory cache for fallback pricing.</param>
    /// <param name="publishEndpoint">The MassTransit publish endpoint.</param>
    /// <param name="logger">The logger.</param>
    public PricingOrchestrator(
        PricingDbContext dbContext,
        IPricingEngine pricingEngine,
        IMemoryCache cache,
        IPublishEndpoint publishEndpoint,
        ILogger<PricingOrchestrator> logger)
    {
        _dbContext = dbContext;
        _pricingEngine = pricingEngine;
        _cache = cache;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PricingResult> CalculatePriceAsync(PricingRequest request, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"PriceFallback_{request.MaterialId}_{request.ManufacturingProcessId}";

        try
        {
            // 1. Get Pricing Configuration
            var config = await _dbContext.PricingConfigurations
                .Where(c => c.MaterialId == request.MaterialId &&
                            c.ManufacturingProcessId == request.ManufacturingProcessId &&
                            c.IsActive &&
                            c.EffectiveFrom <= DateTime.UtcNow &&
                            (c.EffectiveTo == null || c.EffectiveTo >= DateTime.UtcNow))
                .OrderByDescending(c => c.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken);

            if (config == null)
            {
                throw new InvalidOperationException($"No active pricing configuration found for Material: {request.MaterialId} and Process: {request.ManufacturingProcessId}");
            }

            // 2. Perform Calculation
            var result = await _pricingEngine.CalculateAsync(request, config, cancellationToken);

            // 3. Apply Loyalty Discount (Task T013)
            result = await ApplyLoyaltyDiscountAsync(request.CustomerId, result, cancellationToken);

            // 4. Persist Audit Record (Task T012)
            var auditRecord = new PricingAuditRecord
            {
                FileId = request.FileId,
                CustomerId = request.CustomerId,
                InputVolumeCm3 = request.Geometry.VolumeCm3,
                InputSupportVolumeCm3 = request.Geometry.SupportVolumeCm3,
                InputSurfaceAreaCm2 = request.Geometry.SurfaceAreaCm2,
                InputBoundingBoxX = request.Geometry.BoundingBoxX,
                InputBoundingBoxY = request.Geometry.BoundingBoxY,
                InputBoundingBoxZ = request.Geometry.BoundingBoxZ,
                InputIsManifold = request.Geometry.IsManifold,
                InputTriangleCount = request.Geometry.TriangleCount,
                MaterialId = request.MaterialId,
                MaterialCode = request.MaterialCode,
                ManufacturingProcessId = request.ManufacturingProcessId,
                ManufacturingProcessName = request.ManufacturingProcessName,
                Quantity = request.Quantity,
                PricingConfigurationId = config.Id,
                ConfigMaterialPricePerCm3 = config.MaterialPricePerCm3,
                ConfigSupportPricePerCm3 = config.SupportMaterialPricePerCm3,
                ConfigMachineHourlyRate = config.MachineHourlyRate,
                ConfigMarginMultiplier = config.MarginMultiplier,
                Strategy = (EntityPricingStrategy)result.Strategy,
                MLModelVersion = result.MLModelVersion,
                MaterialCost = result.MaterialCost,
                SupportMaterialCost = result.SupportMaterialCost,
                MachineTimeCost = result.MachineTimeCost,
                SetupCost = result.SetupCost,
                ComplexitySurcharge = result.ComplexitySurcharge,
                SubtotalBeforeMargin = result.SubtotalBeforeMargin,
                MarginAmount = result.MarginAmount,
                TotalUnitPrice = result.TotalUnitPrice,
                TotalPrice = result.TotalPrice,
                ConfidenceLevel = result.ConfidenceLevel,
                CurrencyCode = "THB",
                ValidFrom = DateTime.UtcNow,
                ValidUntil = result.ValidUntil,
                CalculatedAt = DateTime.UtcNow,
                CalculatedBySystem = "PricingService",
                CorrelationId = request.CorrelationId,
                CalculationDuration = result.CalculationDuration
            };

            await _dbContext.PricingAuditRecords.AddAsync(auditRecord, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 5. Publish PriceCalculatedEvent (Task T018)
            await _publishEndpoint.Publish(new PriceCalculatedEvent(
                MessageId: Guid.NewGuid(),
                MessageName: nameof(PriceCalculatedEvent),
                MessageType: MessageType.Event,
                MessageVersion: "1.0",
                PublishedBy: "PricingService",
                ConsumedBy: Array.Empty<string>(),
                CorrelationId: Guid.NewGuid(),
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
                    Quantity: request.Quantity,
                    InputVolumeCm3: (double)request.Geometry.VolumeCm3,
                    InputSupportVolumeCm3: (double)request.Geometry.SupportVolumeCm3,
                    InputSurfaceAreaCm2: (double)request.Geometry.SurfaceAreaCm2,
                    Strategy: result.Strategy.ToString(),
                    MlModelVersion: result.MLModelVersion,
                    ConfidenceLevel: (double)result.ConfidenceLevel,
                    PricingConfigurationId: config.Id,
                    Breakdown: new PriceCalculatedEventPayloadBreakdown(
                        MaterialCost: (double)result.MaterialCost,
                        SupportCost: (double)result.SupportMaterialCost,
                        MachineTimeCost: (double)result.MachineTimeCost,
                        SetupCost: (double)result.SetupCost,
                        ComplexitySurcharge: (double)result.ComplexitySurcharge,
                        SubtotalBeforeMargin: (double)result.SubtotalBeforeMargin,
                        MarginAmount: (double)result.MarginAmount,
                        TotalPrice: (double)result.TotalPrice),
                    TotalUnitPrice: (double)result.TotalUnitPrice,
                    TotalPrice: (double)result.TotalPrice,
                    Currency: auditRecord.CurrencyCode,
                    ValidUntil: new DateTimeOffset(result.ValidUntil, TimeSpan.Zero),
                    CalculatedAt: new DateTimeOffset(auditRecord.CalculatedAt, TimeSpan.Zero))),
                cancellationToken);

            // 6. Update fallback cache (Task T017)
            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromDays(7))
                .SetSize(1));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pricing calculation failed for file {FileId}. Attempting fallback.", request.FileId);

            if (_cache.TryGetValue(cacheKey, out PricingResult? cachedResult))
            {
                _logger.LogInformation("Using stale fallback price for file {FileId}", request.FileId);
                return cachedResult!;
            }

            throw;
        }
    }

    private async Task<PricingResult> ApplyLoyaltyDiscountAsync(Guid customerId, PricingResult result, CancellationToken cancellationToken)
    {
        // Task T013: Add Loyalty Tier lookup and discount calculation logic.
        // In a real scenario, this would call a CustomerService or lookup from DB.
        // For now, we'll simulate a tier lookup.

        decimal discountPercent = 0;

        // Simulating tier lookup (this could be a DB query or cache hit)
        // Hardcoded simulation for demonstration:
        if (customerId != Guid.Empty)
        {
            // Simulate Gold tier for some IDs, etc.
            // For now, let's just assume 5% discount for all recognized customers as a placeholder
            discountPercent = 5.0m;
        }

        if (discountPercent > 0)
        {
            decimal discountMultiplier = (100m - discountPercent) / 100m;
            decimal newUnitPrice = Math.Round(result.TotalUnitPrice * discountMultiplier, 2);
            decimal newTotalPrice = Math.Round(result.TotalPrice * discountMultiplier, 2);

            return result with
            {
                TotalUnitPrice = newUnitPrice,
                TotalPrice = newTotalPrice
            };
        }

        return result;
    }
}
