using Maliev.PricingService.Api.Interfaces;
using Maliev.PricingService.Data;
using Maliev.PricingService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Maliev.PricingService.Api.Services;

/// <summary>
/// Orchestrates the pricing calculation process.
/// </summary>
public class PricingOrchestrator : IPricingOrchestrator
{
    private readonly PricingDbContext _dbContext;
    private readonly IPricingEngine _pricingEngine;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PricingOrchestrator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PricingOrchestrator"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <param name="pricingEngine">The pricing engine.</param>
    /// <param name="cache">The memory cache for fallback pricing.</param>
    /// <param name="logger">The logger.</param>
    public PricingOrchestrator(
        PricingDbContext dbContext,
        IPricingEngine pricingEngine,
        IMemoryCache cache,
        ILogger<PricingOrchestrator> logger)
    {
        _dbContext = dbContext;
        _pricingEngine = pricingEngine;
        _cache = cache;
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
                TotalUnitPrice = result.TotalUnitPrice,
                TotalPrice = result.TotalPrice,
                CurrencyCode = "THB",
                CalculatedAt = DateTime.UtcNow,
                Strategy = result.Strategy.ToString(),
                PricingConfigurationId = config.Id
            };

            await _dbContext.PricingAuditRecords.AddAsync(auditRecord, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 5. Update fallback cache (Task T017)
            _cache.Set(cacheKey, result, TimeSpan.FromDays(7));

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
