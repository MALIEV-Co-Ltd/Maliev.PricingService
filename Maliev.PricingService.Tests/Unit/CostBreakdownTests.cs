using System.Text.Json;

using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Services;

namespace Maliev.PricingService.Tests.Unit;

/// <summary>
/// Verifies the CostBreakdown contract: SubtotalBeforeMargin equals the sum of its parts,
/// and each calculator correctly itemises its cost components.
/// </summary>
public class CostBreakdownTests
{
    // ── Invariant: SubtotalBeforeMargin == sum of components ──────────────────

    [Fact]
    public void CostBreakdown_SubtotalEqualsComponentSum()
    {
        var bd = new CostBreakdown(
            MaterialCost: 100m,
            SupportMaterialCost: 20m,
            MachineTimeCost: 150m,
            SetupCost: 50m,
            DfmSurcharge: 15m,
            ComplexitySurcharge: 25m,
            SubtotalBeforeMargin: 360m,
            MinimumOrderPriceFloor: 300m);

        Assert.Equal(360m, bd.MaterialCost + bd.SupportMaterialCost + bd.MachineTimeCost
            + bd.SetupCost + bd.DfmSurcharge + bd.ComplexitySurcharge);
    }

    [Fact]
    public void CostBreakdown_FixedDfmAllocation_DoesNotChangeSerializedContract()
    {
        var breakdown = new CostBreakdown(1m, 2m, 3m, 4m, 5m, 6m, 21m, 10m)
        {
            FixedDfmSurcharge = 1m
        };

        var json = JsonSerializer.Serialize(breakdown);

        Assert.DoesNotContain(nameof(CostBreakdown.FixedDfmSurcharge), json, StringComparison.Ordinal);
    }

    // ── FDM: material split into part + support ───────────────────────────────

    [Fact]
    public void FdmCalculator_WithSupport_SupportMaterialCostIsPositive()
    {
        var calc = new FdmPricingCalculator();
        var ctx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 50m, SupportVolumeCm3 = 10m, BoundingBoxZ = 5m },
            MaterialPricePerCm3: 1.0m, MachineHourlyRate: 100m,
            SetupFee: 0m, MinimumOrderPrice: 0m, Dfm: null,
            ProcessParameters: new Dictionary<string, string>());

        var result = calc.Calculate(ctx);

        Assert.True(result.SupportMaterialCost > 0m);
        Assert.True(result.MaterialCost > 0m);
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
    }

    [Fact]
    public void FdmCalculator_DfmThinWalls_PositiveDfmSurcharge()
    {
        var calc = new FdmPricingCalculator();
        var dfm = new DfmMetrics { ThinWallCount = 3 };
        var ctx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 50m, BoundingBoxZ = 5m },
            1.0m, 100m, 0m, 0m, dfm, new Dictionary<string, string>());

        var result = calc.Calculate(ctx);

        Assert.True(result.DfmSurcharge > 0m);
    }

    // ── CNC complexity surcharge fires for high SA/V parts ────────────────────

    [Fact]
    public void CncCalculator_ThinWalledPart_ComplexitySurchargeApplied()
    {
        var calc = new CncPricingCalculator();
        // Thin-walled shell: high SA/V ratio. V=2cm³, SA=100cm², cbrtV≈1.26 → factor≈39.7 > 6.0
        // BoundingBox in mm: 50×50×2 mm → blockVolume=5cm³, removalVolume=3cm³ (non-zero machine time)
        var ctx = new PricingContext(
            new GeometryMetrics
            {
                VolumeCm3 = 2m,
                SurfaceAreaCm2 = 100m,
                BoundingBoxX = 50m,
                BoundingBoxY = 50m,
                BoundingBoxZ = 2m
            },
            MaterialPricePerCm3: 5.0m, MachineHourlyRate: 500m,
            SetupFee: 0m, MinimumOrderPrice: 0m, Dfm: null,
            ProcessParameters: new Dictionary<string, string>());

        var result = calc.Calculate(ctx);

        Assert.True(result.ComplexitySurcharge > 0m, "Expected complexity surcharge for thin-walled part");
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
    }

    [Fact]
    public void CncCalculator_SimpleCubePart_NoComplexitySurcharge()
    {
        var calc = new CncPricingCalculator();
        // Large cube: V=1000cm³, SA=600cm², cbrtV=10 → factor=(600/1000)/10=0.06 < 6.0
        // BoundingBox in mm: 100×100×100 mm → blockVolume=1000cm³ = volumeCm3 (solid block, zero removal)
        var ctx = new PricingContext(
            new GeometryMetrics
            {
                VolumeCm3 = 1000m,
                SurfaceAreaCm2 = 600m,
                BoundingBoxX = 100m,
                BoundingBoxY = 100m,
                BoundingBoxZ = 100m
            },
            MaterialPricePerCm3: 5.0m, MachineHourlyRate: 500m,
            SetupFee: 0m, MinimumOrderPrice: 0m, Dfm: null,
            ProcessParameters: new Dictionary<string, string>());

        var result = calc.Calculate(ctx);

        Assert.Equal(0m, result.ComplexitySurcharge);
    }

    // ── Scanning / Design: zero subTotal, full floor ──────────────────────────

    [Fact]
    public void ScanningCalculator_AlwaysZeroSubtotalWithFloor()
    {
        var calc = new ScanningPricingCalculator();
        var ctx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 9999m },
            9999m, 9999m, 9999m, 2500m, null, new Dictionary<string, string>());

        var result = calc.Calculate(ctx);

        Assert.Equal(0m, result.SubtotalBeforeMargin);
        Assert.Equal(2500m, result.MinimumOrderPriceFloor);
    }

    [Fact]
    public void DesignCalculator_AlwaysZeroSubtotalWithFloor()
    {
        var calc = new DesignPricingCalculator();
        var ctx = new PricingContext(
            new GeometryMetrics(),
            0m, 0m, 0m, 500m, null, new Dictionary<string, string>());

        var result = calc.Calculate(ctx);

        Assert.Equal(0m, result.SubtotalBeforeMargin);
        Assert.Equal(500m, result.MinimumOrderPriceFloor);
    }

    // ── DMLS: support material is full price (solid metal) ────────────────────

    [Fact]
    public void DmlsCalculator_SupportCostMatchesPartMaterialRate()
    {
        var calc = new DmlsPricingCalculator();
        var materialRate = 15m;
        var ctx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 10m, SupportVolumeCm3 = 5m, BoundingBoxZ = 5m },
            materialRate, 0m, 0m, 0m, null, new Dictionary<string, string>());

        var result = calc.Calculate(ctx);

        Assert.Equal(10m * materialRate, result.MaterialCost);
        Assert.Equal(5m * materialRate, result.SupportMaterialCost);
    }
}
