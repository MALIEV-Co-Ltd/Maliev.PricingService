using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.PricingService.Domain.Constants;
using Maliev.PricingService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PricingService.Api.Controllers;

/// <summary>
/// Catalog endpoints for lead time options, volume discount tiers, and bulk pricing.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("pricing/v{version:apiVersion}/catalog")]
public class PricingCatalogController(PricingDbContext db) : ControllerBase
{
    /// <summary>Returns all active lead time options.</summary>
    [HttpGet("lead-times")]
    [RequirePermission(PricingPermissions.CatalogRead)]
    public async Task<ActionResult<IEnumerable<LeadTimeOptionResponse>>> GetLeadTimeOptions(CancellationToken cancellationToken)
    {
        var options = await db.LeadTimeOptions
            .AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.SortOrder)
            .Select(o => new LeadTimeOptionResponse(
                o.Code, o.Name, o.MinBusinessDays, o.MaxBusinessDays, o.PriceMultiplier, o.IsDefault))
            .ToListAsync(cancellationToken);

        return Ok(options);
    }

    /// <summary>Returns all active volume discount tiers.</summary>
    [HttpGet("volume-tiers")]
    [RequirePermission(PricingPermissions.CatalogRead)]
    public async Task<ActionResult<IEnumerable<VolumeDiscountTierResponse>>> GetVolumeDiscountTiers(CancellationToken cancellationToken)
    {
        var tiers = await db.VolumeDiscountTiers
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => new VolumeDiscountTierResponse(t.MinQuantity, t.MaxQuantity, t.DiscountPercent))
            .ToListAsync(cancellationToken);

        return Ok(tiers);
    }

    /// <summary>
    /// Calculates bulk pricing for a range of quantities given a base unit price.
    /// Applies volume discounts and an optional lead time multiplier.
    /// </summary>
    [HttpPost("bulk-pricing")]
    [RequirePermission(PricingPermissions.CalculationsCreate)]
    public async Task<ActionResult<IEnumerable<BulkPriceTierResponse>>> CalculateBulkPricing(
        [FromBody] BulkPricingRequest request, CancellationToken cancellationToken)
    {
        var tiers = await db.VolumeDiscountTiers
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToListAsync(cancellationToken);

        decimal leadTimeMultiplier = 1.0m;
        if (!string.IsNullOrEmpty(request.LeadTimeCode))
        {
            var lt = await db.LeadTimeOptions
                .AsNoTracking()
                .Where(o => o.IsActive && o.Code == request.LeadTimeCode.ToUpperInvariant())
                .FirstOrDefaultAsync(cancellationToken);
            if (lt is not null) leadTimeMultiplier = lt.PriceMultiplier;
        }

        var results = request.Quantities.Select(qty =>
        {
            var tier = tiers.FirstOrDefault(t =>
                t.MinQuantity <= qty && (t.MaxQuantity == null || t.MaxQuantity >= qty));

            var discountPercent = tier?.DiscountPercent ?? 0m;
            var unitPrice = request.BaseUnitPrice * leadTimeMultiplier * (1 - discountPercent / 100m);
            return new BulkPriceTierResponse(qty, Math.Round(unitPrice, 2), Math.Round(unitPrice * qty, 2), discountPercent);
        }).ToList();

        return Ok(results);
    }
}

/// <summary>Lead time option response.</summary>
public record LeadTimeOptionResponse(string Code, string Name, int MinDays, int MaxDays, decimal PriceMultiplier, bool IsDefault);

/// <summary>Volume discount tier response.</summary>
public record VolumeDiscountTierResponse(int MinQuantity, int? MaxQuantity, decimal DiscountPercent);

/// <summary>Bulk pricing request.</summary>
public class BulkPricingRequest
{
    /// <summary>Base unit price from the calculate endpoint.</summary>
    public decimal BaseUnitPrice { get; set; }
    /// <summary>Quantities to calculate pricing for.</summary>
    public int[] Quantities { get; set; } = [1, 2, 5, 10, 25, 50, 100];
    /// <summary>Optional lead time code (e.g. "STANDARD").</summary>
    public string? LeadTimeCode { get; set; }
}

/// <summary>Single tier in a bulk pricing response.</summary>
public record BulkPriceTierResponse(int Quantity, decimal UnitPrice, decimal Total, decimal DiscountPercent);
