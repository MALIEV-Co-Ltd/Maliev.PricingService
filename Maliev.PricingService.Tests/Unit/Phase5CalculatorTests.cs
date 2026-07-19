using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Services;

namespace Maliev.PricingService.Tests.Unit;

/// <summary>
/// Verifies Phase 5 per-time and per-feature calculators produce correct cost breakdowns.
/// </summary>
public class Phase5CalculatorTests
{
    private static PricingContext Ctx(
        decimal volumeCm3 = 10m,
        decimal surfaceAreaCm2 = 50m,
        decimal bbX = 30m, decimal bbY = 30m, decimal bbZ = 10m,
        decimal materialPricePerCm3 = 1.0m,
        decimal machineHourlyRate = 600m,
        decimal setupFee = 200m,
        decimal minimumOrderPrice = 0m,
        Dictionary<string, string>? pp = null)
        => new(
            new GeometryMetrics
            {
                VolumeCm3 = volumeCm3, SurfaceAreaCm2 = surfaceAreaCm2,
                BoundingBoxX = bbX, BoundingBoxY = bbY, BoundingBoxZ = bbZ
            },
            materialPricePerCm3, machineHourlyRate, setupFee, minimumOrderPrice,
            null, pp ?? new Dictionary<string, string>());

    // ── EdmPricingCalculator ──────────────────────────────────────────────────

    [Fact]
    public void Edm_SubtotalEqualsComponentSum()
    {
        var calc = new EdmPricingCalculator();
        var result = calc.Calculate(Ctx());
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.MachineTimeCost + result.SetupCost);
    }

    [Fact]
    public void Edm_MachineTimeBasedOnMrr()
    {
        var calc = new EdmPricingCalculator();
        // 4 cm³ / 2 cm³/h = 2h × 600 = 1200
        var ctx = Ctx(volumeCm3: 4m, machineHourlyRate: 600m, setupFee: 0m, pp: new()
        {
            ["MrrCm3PerHour"] = "2",
            ["ElectrodeCount"] = "0",
            ["ElectrodeCostFlat"] = "0"
        });
        var result = calc.Calculate(ctx);
        Assert.Equal(1200m, result.MachineTimeCost);
    }

    [Fact]
    public void Edm_ElectrodeCostAddedAsMaterialCost()
    {
        var calc = new EdmPricingCalculator();
        var ctx = Ctx(volumeCm3: 2m, setupFee: 0m, pp: new()
        {
            ["ElectrodeCount"] = "3",
            ["ElectrodeCostFlat"] = "500",
            ["MrrCm3PerHour"] = "999"  // negligible machine time
        });
        var result = calc.Calculate(ctx);
        Assert.Equal(1500m, result.MaterialCost);
    }

    // ── GrindingPricingCalculator ─────────────────────────────────────────────

    [Fact]
    public void Grinding_SubtotalEqualsComponentSum()
    {
        var calc = new GrindingPricingCalculator();
        var result = calc.Calculate(Ctx());
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MachineTimeCost + result.SetupCost);
    }

    [Fact]
    public void Grinding_PassCountMultipliesMachineTime()
    {
        var calc = new GrindingPricingCalculator();
        var ctx1 = Ctx(surfaceAreaCm2: 100m, materialPricePerCm3: 2m, setupFee: 0m, pp: new() { ["PassCount"] = "1" });
        var ctx2 = Ctx(surfaceAreaCm2: 100m, materialPricePerCm3: 2m, setupFee: 0m, pp: new() { ["PassCount"] = "3" });
        Assert.Equal(calc.Calculate(ctx1).MachineTimeCost * 3m, calc.Calculate(ctx2).MachineTimeCost);
    }

    // ── WeldingPricingCalculator ──────────────────────────────────────────────

    [Fact]
    public void Welding_SubtotalEqualsComponentSum()
    {
        var calc = new WeldingPricingCalculator();
        var result = calc.Calculate(Ctx(pp: new() { ["WeldLengthMm"] = "100" }));
        Assert.Equal(result.SubtotalBeforeMargin, result.MachineTimeCost + result.SetupCost);
    }

    [Fact]
    public void Welding_ZeroWeldLength_OnlyPrepAndSetup()
    {
        var calc = new WeldingPricingCalculator();
        var ctx = Ctx(machineHourlyRate: 400m, setupFee: 0m, pp: new()
        {
            ["WeldLengthMm"] = "0",
            ["PreparationHoursFlat"] = "2"
        });
        var result = calc.Calculate(ctx);
        // Only prep: 2h × 400 = 800
        Assert.Equal(800m, result.MachineTimeCost);
    }

    // ── StdMetalFabPricingCalculator ──────────────────────────────────────────

    [Fact]
    public void StdMetalFab_SubtotalEqualsComponentSum()
    {
        var calc = new StdMetalFabPricingCalculator();
        var ctx = Ctx(pp: new() { ["WeightKg"] = "5", ["CutLengthMm"] = "200", ["BendCount"] = "3" });
        var result = calc.Calculate(ctx);
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.MachineTimeCost + result.SetupCost);
    }

    [Fact]
    public void StdMetalFab_MaterialCostIsWeightTimesPrice()
    {
        var calc = new StdMetalFabPricingCalculator();
        var ctx = Ctx(materialPricePerCm3: 10m, setupFee: 0m, pp: new()
        {
            ["WeightKg"] = "5",
            ["CutLengthMm"] = "0",
            ["BendCount"] = "0"
        });
        var result = calc.Calculate(ctx);
        Assert.Equal(50m, result.MaterialCost);
    }

    // ── DeviationAnalysisPricingCalculator ────────────────────────────────────

    [Fact]
    public void DeviationAnalysis_SubtotalEqualsComponentSum()
    {
        var calc = new DeviationAnalysisPricingCalculator();
        var result = calc.Calculate(Ctx());
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.MachineTimeCost + result.SetupCost);
    }

    [Fact]
    public void DeviationAnalysis_AnalystHoursAddedToMachineTimeCost()
    {
        var calc = new DeviationAnalysisPricingCalculator();
        var ctx = Ctx(machineHourlyRate: 300m, setupFee: 0m, pp: new()
        {
            ["AnalystHoursFlat"] = "2",
            ["PointCountThousands"] = "0"
        });
        var result = calc.Calculate(ctx);
        Assert.Equal(600m, result.MachineTimeCost);
    }

    // ── ChinaOutsourcingPricingCalculator ─────────────────────────────────────

    [Fact]
    public void ChinaOutsourcing_NoVendorQuote_ReturnsZeroSubtotal()
    {
        var calc = new ChinaOutsourcingPricingCalculator();
        var result = calc.Calculate(Ctx());
        Assert.Equal(0m, result.MaterialCost);
    }

    [Fact]
    public void ChinaOutsourcing_DefaultMarkupIsTwo()
    {
        var calc = new ChinaOutsourcingPricingCalculator();
        var ctx = Ctx(materialPricePerCm3: 0m, setupFee: 0m, pp: new()
        {
            ["VendorQuoteAmount"] = "3650"
        });
        var result = calc.Calculate(ctx);
        // 3650 × 2.0 = 7300
        Assert.Equal(7300m, result.MaterialCost);
        Assert.Equal(7300m, result.SubtotalBeforeMargin);
    }

    [Fact]
    public void ChinaOutsourcing_MarkupOverrideApplied()
    {
        var calc = new ChinaOutsourcingPricingCalculator();
        var ctx = Ctx(setupFee: 0m, pp: new()
        {
            ["VendorQuoteAmount"] = "1000",
            ["VendorQuoteMarkupOverride"] = "1.8"
        });
        var result = calc.Calculate(ctx);
        Assert.Equal(1800m, result.MaterialCost);
    }

    [Fact]
    public void ChinaOutsourcing_ConfigMarkupUsedWhenNoOverride()
    {
        var calc = new ChinaOutsourcingPricingCalculator();
        // MaterialPricePerCm3 = 1.5 used as markup multiplier when set
        var ctx = Ctx(materialPricePerCm3: 1.5m, setupFee: 0m, pp: new()
        {
            ["VendorQuoteAmount"] = "1000"
        });
        var result = calc.Calculate(ctx);
        Assert.Equal(1500m, result.MaterialCost);
    }
}
