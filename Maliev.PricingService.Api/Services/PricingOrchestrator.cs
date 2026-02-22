namespace Maliev.PricingService.Api.Services;

using Maliev.MessagingContracts.Contracts.Pricing;
using Maliev.PricingService.Api.Interfaces;
using Maliev.PricingService.Api.Clients;
using Maliev.PricingService.Data;
using Maliev.PricingService.Data.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EntityPricingStrategy = Maliev.PricingService.Data.Entities.PricingStrategy;

/// <summary>
/// Orchestrates the pricing calculation process.
/// </summary>
public class PricingOrchestrator : IPricingOrchestrator
{
    private readonly PricingDbContext _dbContext;
    private readonly IPricingEngine _pricingEngine;
    private readonly IMaterialServiceClient _materialClient;
    private readonly IMemoryCache _cache;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PricingOrchestrator> _logger;
    private readonly MachineRates _machineRates;

    /// <summary>
    /// Initializes a new instance of the <see cref="PricingOrchestrator"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="pricingEngine">The pricing engine.</param>
    /// <param name="materialClient">The material service client.</param>
    /// <param name="cache">The memory cache for fallback pricing.</param>
    /// <param name="publishEndpoint">The MassTransit publish endpoint.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="machineRatesOptions">The machine rates from configuration.</param>
    public PricingOrchestrator(
        PricingDbContext dbContext,
        IPricingEngine pricingEngine,
        IMaterialServiceClient materialClient,
        IMemoryCache cache,
        IPublishEndpoint publishEndpoint,
        ILogger<PricingOrchestrator> logger,
        IOptions<MachineRates> machineRatesOptions)
    {
        _dbContext = dbContext;
        _pricingEngine = pricingEngine;
        _materialClient = materialClient;
        _cache = cache;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _machineRates = machineRatesOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<PricingResult> CalculatePriceAsync(PricingRequest request, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"PriceFallback_{request.MaterialId}_{request.Technology}";

        try
        {
            // 1. Get Material Data from MaterialService
            var materialDto = await _materialClient.GetMaterialAsync(request.MaterialId, cancellationToken)
                ?? throw new InvalidOperationException($"Material {request.MaterialId} not found");

            var materialData = MapToMaterialData(materialDto);

            // 2. Perform Calculation
            var result = await _pricingEngine.CalculateAsync(request, materialData, _machineRates, cancellationToken);

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
                Technology = request.Technology.ToString(),
                ManufacturingProcessId = request.ManufacturingProcessId,
                ManufacturingProcessName = request.ManufacturingProcessName,
                Quantity = request.Quantity,
                Strategy = (EntityPricingStrategy)result.Strategy,
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
            await _publishEndpoint.Publish(new PriceCalculatedEvent
            {
                PricingAuditId = auditRecord.Id,
                FileId = request.FileId,
                CustomerId = request.CustomerId,
                MaterialId = request.MaterialId,
                ProcessId = request.ManufacturingProcessId ?? Guid.Empty,
                Quantity = request.Quantity,
                InputVolumeCm3 = request.Geometry.VolumeCm3,
                InputSupportVolumeCm3 = request.Geometry.SupportVolumeCm3,
                InputSurfaceAreaCm2 = request.Geometry.SurfaceAreaCm2,
                Strategy = result.Strategy.ToString(),
                ConfidenceLevel = result.ConfidenceLevel,
                Breakdown = new PriceBreakdownContract
                {
                    MaterialCost = result.MaterialCost,
                    SupportCost = result.SupportMaterialCost,
                    MachineTimeCost = result.MachineTimeCost,
                    SetupCost = result.SetupCost,
                    ComplexitySurcharge = result.ComplexitySurcharge,
                    SubtotalBeforeMargin = result.SubtotalBeforeMargin,
                    MarginAmount = result.MarginAmount,
                    TotalPrice = result.TotalPrice
                },
                TotalUnitPrice = result.TotalUnitPrice,
                TotalPrice = result.TotalPrice,
                Currency = auditRecord.CurrencyCode,
                ValidUntil = result.ValidUntil,
                CalculatedAt = auditRecord.CalculatedAt
            }, cancellationToken);

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

    private MaterialData MapToMaterialData(MaterialDto dto)
    {
        return new MaterialData
        {
            Density = dto.DensityGramPerCm3,
            CostPerKg = dto.CostPerKg,
            FdmVolumetricFlowRate = GetParam(dto.ProcessParameters, "FdmVolumetricFlowRate", 15m),
            FdmMinLayerTime = GetParam(dto.ProcessParameters, "FdmMinLayerTime", 5m),
            SlaLayerExposure = GetParam(dto.ProcessParameters, "SlaLayerExposure", 2.5m),
            SlaLiftTime = GetParam(dto.ProcessParameters, "SlaLiftTime", 1.5m),
            CncMachinabilityRating = GetParam(dto.ProcessParameters, "CncMachinabilityRating", 0.8m)
        };
    }

    private static decimal GetParam(Dictionary<string, string> parameters, string key, decimal defaultValue)
    {
        if (parameters.TryGetValue(key, out var value) && decimal.TryParse(value, out var result))
        {
            return result;
        }
        return defaultValue;
    }

    private async Task<PricingResult> ApplyLoyaltyDiscountAsync(Guid customerId, PricingResult result, CancellationToken cancellationToken)
    {
        // Simulation of tier lookup
        decimal discountPercent = 0;
        if (customerId != Guid.Empty)
        {
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
