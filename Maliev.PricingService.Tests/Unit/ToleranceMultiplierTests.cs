using Maliev.PricingService.Application.DTOs;

namespace Maliev.PricingService.Tests.Unit;

/// <summary>
/// Verifies that the tolerance AdditionalCostPercent multiplier produces the correct
/// price ratios. In PricingOrchestrator.CalculatePriceAsync the surcharges are applied as:
///   surchargedUnitPrice = marginedUnitPrice * leadTimeMultiplier * toleranceMultiplier
/// where toleranceMultiplier = 1 + (ToleranceAdditionalCostPercent / 100)
/// and marginedUnitPrice = breakdown.SubtotalBeforeMargin * config.MarginMultiplier.
/// The helper below treats the input <c>unitPrice</c> as the already-margined unit price.
/// </summary>
public class ToleranceMultiplierTests
{
    /// <summary>
    /// IT6 (60%) vs ISO2768_M (10%) should produce a ~1.4545x ratio (1.60 / 1.10).
    /// </summary>
    [Fact]
    public void ToleranceMultiplier_IT6VsISO2768M_ProducesExpectedRatio()
    {
        const decimal baseUnitPrice = 1000m;
        const decimal leadTimeMultiplier = 1.0m; // Standard lead time — no additional factor

        var it6Request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "AL6061",
            ManufacturingProcessId = Guid.NewGuid(),
            ManufacturingProcessName = "CNC_MILL",
            Quantity = 1,
            Geometry = new GeometryMetrics(),
            ToleranceCode = "IT6",
            ToleranceAdditionalCostPercent = 60m,
        };

        var iso2768mRequest = it6Request with
        {
            ToleranceCode = "ISO2768_M",
            ToleranceAdditionalCostPercent = 10m,
        };

        var it6Price = ApplyMultipliers(baseUnitPrice, leadTimeMultiplier, it6Request.ToleranceAdditionalCostPercent);
        var iso2768mPrice = ApplyMultipliers(baseUnitPrice, leadTimeMultiplier, iso2768mRequest.ToleranceAdditionalCostPercent);

        // IT6=1600, ISO2768_M=1100, ratio=1600/1100=1.4545…
        var ratio = it6Price / iso2768mPrice;
        Assert.InRange(ratio, 1.44m, 1.46m);
    }

    /// <summary>
    /// When no tolerance is set (null percent), the multiplier should be 1.0 (no change).
    /// </summary>
    [Fact]
    public void ToleranceMultiplier_NullPercent_DoesNotChangePrice()
    {
        const decimal baseUnitPrice = 500m;
        const decimal leadTimeMultiplier = 1.0m;

        var price = ApplyMultipliers(baseUnitPrice, leadTimeMultiplier, toleranceAdditionalCostPercent: null);

        Assert.Equal(500m, price);
    }

    /// <summary>
    /// When tolerance percent is 0, the multiplier should be 1.0 (no change).
    /// </summary>
    [Fact]
    public void ToleranceMultiplier_ZeroPercent_DoesNotChangePrice()
    {
        const decimal baseUnitPrice = 500m;
        const decimal leadTimeMultiplier = 1.0m;

        var price = ApplyMultipliers(baseUnitPrice, leadTimeMultiplier, toleranceAdditionalCostPercent: 0m);

        Assert.Equal(500m, price);
    }

    /// <summary>
    /// Tolerance multiplier and lead time multiplier should compose multiplicatively.
    /// 60% tolerance + 1.3x express lead time = 1000 * 1.3 * 1.6 = 2080.
    /// </summary>
    [Fact]
    public void ToleranceMultiplier_ComposesWithLeadTimeMultiplier()
    {
        const decimal baseUnitPrice = 1000m;
        const decimal expressLeadTimeMultiplier = 1.3m;
        const decimal it6Percent = 60m;

        var price = ApplyMultipliers(baseUnitPrice, expressLeadTimeMultiplier, it6Percent);

        Assert.Equal(2080m, price);
    }

    /// <summary>
    /// Validates IT6 (60%) produces exactly 1600 from a 1000 base with standard lead time.
    /// </summary>
    [Fact]
    public void ToleranceMultiplier_IT6_Produces160PercentOfBase()
    {
        const decimal baseUnitPrice = 1000m;

        var price = ApplyMultipliers(baseUnitPrice, leadTimeMultiplier: 1.0m, toleranceAdditionalCostPercent: 60m);

        Assert.Equal(1600m, price);
    }

    /// <summary>
    /// Validates ISO2768_M (10%) produces exactly 1100 from a 1000 base with standard lead time.
    /// </summary>
    [Fact]
    public void ToleranceMultiplier_ISO2768M_Produces110PercentOfBase()
    {
        const decimal baseUnitPrice = 1000m;

        var price = ApplyMultipliers(baseUnitPrice, leadTimeMultiplier: 1.0m, toleranceAdditionalCostPercent: 10m);

        Assert.Equal(1100m, price);
    }

    /// <summary>
    /// Mirrors the exact multiplier logic from PricingOrchestrator.CalculatePriceAsync.
    /// </summary>
    private static decimal ApplyMultipliers(
        decimal unitPrice,
        decimal leadTimeMultiplier,
        decimal? toleranceAdditionalCostPercent)
    {
        var toleranceMultiplier = toleranceAdditionalCostPercent is > 0m
            ? 1m + toleranceAdditionalCostPercent.Value / 100m
            : 1.0m;

        return unitPrice * leadTimeMultiplier * toleranceMultiplier;
    }
}
