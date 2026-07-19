using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Services;

namespace Maliev.PricingService.Tests.Unit;

/// <summary>
/// Verifies Phase 4 per-volume and per-area calculators produce correct cost breakdowns.
/// </summary>
public class Phase4CalculatorTests
{
    private static PricingContext Ctx(
        decimal volumeCm3 = 50m,
        decimal surfaceAreaCm2 = 100m,
        decimal bbX = 50m, decimal bbY = 50m, decimal bbZ = 20m,
        decimal materialPricePerCm3 = 1.0m,
        decimal machineHourlyRate = 200m,
        decimal setupFee = 100m,
        decimal minimumOrderPrice = 0m,
        Dictionary<string, string>? pp = null)
        => new(
            new GeometryMetrics
            {
                VolumeCm3 = volumeCm3,
                SurfaceAreaCm2 = surfaceAreaCm2,
                BoundingBoxX = bbX, BoundingBoxY = bbY, BoundingBoxZ = bbZ
            },
            materialPricePerCm3, machineHourlyRate, setupFee, minimumOrderPrice,
            null, pp ?? new Dictionary<string, string>());

    // ── SheetMetalPricingCalculator ────────────────────────────────────────────

    [Fact]
    public void SheetMetal_SubtotalEqualsComponentSum()
    {
        var calc = new SheetMetalPricingCalculator();
        var result = calc.Calculate(Ctx());
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.MachineTimeCost + result.SetupCost
            + result.DfmSurcharge + result.ComplexitySurcharge);
    }

    [Fact]
    public void SheetMetal_WithCutAndBendParams_UsesExplicitCosts()
    {
        var calc = new SheetMetalPricingCalculator();
        var ctx = Ctx(surfaceAreaCm2: 200m, materialPricePerCm3: 2.0m, pp: new()
        {
            ["CutLengthMm"] = "100",
            ["BendCount"] = "4",
            ["SupportMaterialPricePerCm3"] = "0.5",
            ["BendPriceEach"] = "50"
        });
        var result = calc.Calculate(ctx);

        decimal expectedMaterial = 200m * 2.0m;
        decimal expectedMachine = 100m * 0.5m + 4 * 50m;
        Assert.Equal(expectedMaterial, result.MaterialCost);
        Assert.Equal(expectedMachine, result.MachineTimeCost);
    }

    [Fact]
    public void SheetMetal_NoCutBendParams_UsesFallbackMachineTime()
    {
        var calc = new SheetMetalPricingCalculator();
        var ctx = Ctx(surfaceAreaCm2: 200m);
        var result = calc.Calculate(ctx);
        Assert.True(result.MachineTimeCost > 0m);
    }

    // ── InjectionMouldingPricingCalculator ────────────────────────────────────

    [Fact]
    public void InjectionMoulding_SubtotalEqualsComponentSum()
    {
        var calc = new InjectionMouldingPricingCalculator();
        var result = calc.Calculate(Ctx());
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.MachineTimeCost + result.SetupCost);
    }

    [Fact]
    public void InjectionMoulding_ScrapAddedToMaterialCost()
    {
        var calc = new InjectionMouldingPricingCalculator();
        var ctx = Ctx(volumeCm3: 10m, materialPricePerCm3: 5m, setupFee: 0m, pp: new()
        {
            ["ScrapPct"] = "0.1",
            ["CycleSecondsPerCm3"] = "1.0"
        });
        var result = calc.Calculate(ctx);
        // materialCost = 10 * 5 * 1.1 = 55
        Assert.Equal(55m, result.MaterialCost);
    }

    [Fact]
    public void InjectionMoulding_ToolingFlatAddedToSetupCost()
    {
        var calc = new InjectionMouldingPricingCalculator();
        var ctx = Ctx(volumeCm3: 10m, setupFee: 200m, pp: new()
        {
            ["ToolingCostFlat"] = "1000"
        });
        var result = calc.Calculate(ctx);
        Assert.Equal(1200m, result.SetupCost);
    }

    // ── SiliconeCastingPricingCalculator ──────────────────────────────────────

    [Fact]
    public void SiliconeCasting_SubtotalEqualsComponentSum()
    {
        var calc = new SiliconeCastingPricingCalculator();
        var result = calc.Calculate(Ctx());
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.MachineTimeCost + result.SetupCost);
    }

    [Fact]
    public void SiliconeCasting_WasteAddedToMaterialCost()
    {
        var calc = new SiliconeCastingPricingCalculator();
        var ctx = Ctx(volumeCm3: 100m, materialPricePerCm3: 1m, setupFee: 0m, pp: new()
        {
            ["WastePct"] = "0.25",
            ["CureHoursPerCm3"] = "0"
        });
        var result = calc.Calculate(ctx);
        // materialCost = 100 * 1 * 1.25 = 125
        Assert.Equal(125m, result.MaterialCost);
    }

    // ── ManualInjectionMouldingPricingCalculator ──────────────────────────────

    [Fact]
    public void ManualIM_OperatorRateAddedToMachineRate()
    {
        var calc = new ManualInjectionMouldingPricingCalculator();
        var ctx = Ctx(volumeCm3: 10m, machineHourlyRate: 100m, setupFee: 0m, pp: new()
        {
            ["OperatorRatePerHour"] = "200",
            ["CycleSecondsPerCm3"] = "3600",  // 1 hour per cm³
            ["ScrapPct"] = "0"
        });
        var result = calc.Calculate(ctx);
        // machineTimeCost = 10cm³ * 3600s/cm³ / 3600s/h * (100+200) = 10 * 300 = 3000
        Assert.Equal(3000m, result.MachineTimeCost);
    }

    // ── PaintingPricingCalculator ─────────────────────────────────────────────

    [Fact]
    public void Painting_LayerCountMultipliesMaterialCost()
    {
        var calc = new PaintingPricingCalculator();
        var ctx = Ctx(surfaceAreaCm2: 100m, materialPricePerCm3: 2m, setupFee: 0m, pp: new()
        {
            ["LayerCount"] = "3",
            ["CureHoursPerSqm"] = "0"
        });
        var result = calc.Calculate(ctx);
        // materialCost = 100 * 2 * 3 = 600
        Assert.Equal(600m, result.MaterialCost);
    }

    [Fact]
    public void Painting_DefaultLayerCountIsTwo()
    {
        var calc1 = new PaintingPricingCalculator();
        var calc2 = new PaintingPricingCalculator();
        var ctxDefault = Ctx(surfaceAreaCm2: 100m, materialPricePerCm3: 1m, setupFee: 0m,
            pp: new() { ["CureHoursPerSqm"] = "0" });
        var ctxExplicit = Ctx(surfaceAreaCm2: 100m, materialPricePerCm3: 1m, setupFee: 0m,
            pp: new() { ["LayerCount"] = "2", ["CureHoursPerSqm"] = "0" });

        Assert.Equal(calc1.Calculate(ctxDefault).MaterialCost,
                     calc2.Calculate(ctxExplicit).MaterialCost);
    }

    // ── SurfaceFinishingPricingCalculator ─────────────────────────────────────

    [Fact]
    public void SurfaceFinishing_MaterialCostIsSurfaceAreaTimesPrice()
    {
        var calc = new SurfaceFinishingPricingCalculator();
        var ctx = Ctx(surfaceAreaCm2: 200m, materialPricePerCm3: 3m, setupFee: 50m);
        var result = calc.Calculate(ctx);

        Assert.Equal(200m * 3m, result.MaterialCost);
        Assert.Equal(0m, result.MachineTimeCost);
        Assert.Equal(50m, result.SetupCost);
        Assert.Equal(650m, result.SubtotalBeforeMargin);
    }

    [Fact]
    public void SurfaceFinishing_FinishMultiplierScalesMaterialCost()
    {
        var calc = new SurfaceFinishingPricingCalculator();
        var ctx = Ctx(surfaceAreaCm2: 100m, materialPricePerCm3: 1m, setupFee: 0m,
            pp: new() { ["FinishMultiplier"] = "1.5" });
        var result = calc.Calculate(ctx);
        Assert.Equal(150m, result.MaterialCost);
    }
}
