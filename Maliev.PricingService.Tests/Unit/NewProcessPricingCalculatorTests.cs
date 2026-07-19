using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Services;
using Maliev.PricingService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.PricingService.Tests.Unit;

// ── CNC Milling ───────────────────────────────────────────────────────────────

public class CncMillPricingCalculatorTests
{
    private readonly CncMillPricingCalculator _calculator = new();

    private static PricingContext Ctx(
        decimal volume = 50m, decimal setup = 500m, decimal minOrder = 2500m,
        DfmMetrics? dfm = null, Dictionary<string, string>? p = null)
        => new(
            new GeometryMetrics
            {
                // BoundingBox in mm: 40×40×40 mm = 64 cm³ block, part volume 50 cm³ → 14 cm³ removal
                VolumeCm3 = volume, SurfaceAreaCm2 = 100m,
                BoundingBoxX = 40m, BoundingBoxY = 40m, BoundingBoxZ = 40m
            },
            MaterialPricePerCm3: 5.0m,
            MachineHourlyRate: 500m,
            SetupFee: setup,
            MinimumOrderPrice: minOrder,
            Dfm: dfm,
            ProcessParameters: p ?? new Dictionary<string, string>());

    [Fact]
    public void TechnologyName_IsCncMill()
        => Assert.Equal("CNC_MILL", _calculator.TechnologyName);

    [Fact]
    public void Calculate_ValidInputs_BreakdownIsConsistent()
    {
        var result = _calculator.Calculate(Ctx());

        Assert.True(result.SubtotalBeforeMargin > 0m);
        Assert.Equal(2500m, result.MinimumOrderPriceFloor);
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
    }

    [Fact]
    public void Calculate_EmptyGeometry_PreservesFloor()
    {
        var ctx = new PricingContext(
            new GeometryMetrics(),
            5.0m, 500m, 0m, 2500m, null,
            new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        Assert.Equal(2500m, result.MinimumOrderPriceFloor);
    }

    [Fact]
    public void Calculate_WithDfmSharpCorners_AppliesSurcharge()
    {
        var clean = Ctx(dfm: new DfmMetrics { SharpCornerCount = 0 }, setup: 0m, minOrder: 0m);
        var corners = Ctx(dfm: new DfmMetrics { SharpCornerCount = 5 }, setup: 0m, minOrder: 0m);

        var without = _calculator.Calculate(clean);
        var with = _calculator.Calculate(corners);

        Assert.True(with.DfmSurcharge > without.DfmSurcharge);
    }

    [Fact]
    public void Calculate_WithDfmUndercuts_AppliesUndercutSurcharge()
    {
        var clean = Ctx(dfm: new DfmMetrics { HasUndercuts = false }, setup: 0m, minOrder: 0m);
        var undercut = Ctx(dfm: new DfmMetrics { HasUndercuts = true }, setup: 0m, minOrder: 0m);

        var without = _calculator.Calculate(clean);
        var with = _calculator.Calculate(undercut);

        Assert.True(with.DfmSurcharge > without.DfmSurcharge);
    }
}

// ── CNC Turning ───────────────────────────────────────────────────────────────

public class CncTurnPricingCalculatorTests
{
    private readonly CncTurnPricingCalculator _calculator = new();

    private static PricingContext Ctx(
        decimal volume = 40m, decimal setup = 500m, decimal minOrder = 2500m,
        DfmMetrics? dfm = null, Dictionary<string, string>? p = null)
        => new(
            new GeometryMetrics
            {
                // BoundingBox in mm: 40×40×40 mm = 64 cm³ block, part volume 40 cm³ → 24 cm³ removal
                VolumeCm3 = volume, SurfaceAreaCm2 = 80m,
                BoundingBoxX = 40m, BoundingBoxY = 40m, BoundingBoxZ = 40m
            },
            MaterialPricePerCm3: 5.0m,
            MachineHourlyRate: 500m,
            SetupFee: setup,
            MinimumOrderPrice: minOrder,
            Dfm: dfm,
            ProcessParameters: p ?? new Dictionary<string, string>());

    [Fact]
    public void TechnologyName_IsCncTurn()
        => Assert.Equal("CNC_TURN", _calculator.TechnologyName);

    [Fact]
    public void Calculate_ValidInputs_BreakdownIsConsistent()
    {
        var result = _calculator.Calculate(Ctx());

        Assert.True(result.SubtotalBeforeMargin > 0m);
        Assert.Equal(2500m, result.MinimumOrderPriceFloor);
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
    }

    [Fact]
    public void Calculate_EmptyGeometry_PreservesFloor()
    {
        var ctx = new PricingContext(
            new GeometryMetrics(),
            5.0m, 500m, 0m, 2500m, null,
            new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        Assert.Equal(2500m, result.MinimumOrderPriceFloor);
    }

    [Fact]
    public void Calculate_WithUndercuts_AppliesUndercutSurcharge()
    {
        var without = _calculator.Calculate(Ctx(dfm: new DfmMetrics { HasUndercuts = false }, setup: 0m, minOrder: 0m));
        var with = _calculator.Calculate(Ctx(dfm: new DfmMetrics { HasUndercuts = true }, setup: 0m, minOrder: 0m));

        Assert.True(with.DfmSurcharge > without.DfmSurcharge);
    }

    [Fact]
    public void Calculate_CustomMrr_AffectsMachineCost()
    {
        var defaultResult = _calculator.Calculate(Ctx(setup: 0m, minOrder: 0m));
        var slowMrr = _calculator.Calculate(Ctx(setup: 0m, minOrder: 0m,
            p: new Dictionary<string, string> { ["MRR"] = "20" }));

        Assert.True(slowMrr.MachineTimeCost > defaultResult.MachineTimeCost);
    }
}

// ── SLS ───────────────────────────────────────────────────────────────────────

public class SlsPricingCalculatorTests
{
    private readonly SlsPricingCalculator _calculator = new();

    [Fact]
    public void TechnologyName_IsSls()
        => Assert.Equal("SLS", _calculator.TechnologyName);

    [Fact]
    public void Calculate_ValidInputs_BreakdownIsConsistent()
    {
        var ctx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 50m, BoundingBoxZ = 5m },
            0.8m, 120m, 200m, 500m, null, new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        Assert.True(result.SubtotalBeforeMargin > 0m);
        Assert.Equal(500m, result.MinimumOrderPriceFloor);
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
    }

    [Fact]
    public void Calculate_ZeroVolume_PreservesFloor()
    {
        var ctx = new PricingContext(
            new GeometryMetrics(),
            0.8m, 120m, 0m, 500m, null, new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        Assert.Equal(500m, result.MinimumOrderPriceFloor);
    }

    [Fact]
    public void Calculate_TallerPart_CostsMoreDueToLayerCount()
    {
        var makeCtx = (decimal z) => new PricingContext(
            new GeometryMetrics { VolumeCm3 = 50m, BoundingBoxZ = z },
            0.8m, 120m, 0m, 0m, null, new Dictionary<string, string>());

        var shortPart = _calculator.Calculate(makeCtx(5m));
        var tallPart = _calculator.Calculate(makeCtx(50m));

        Assert.True(tallPart.SubtotalBeforeMargin > shortPart.SubtotalBeforeMargin);
    }

    [Fact]
    public void Calculate_PowderRefreshOverheadIncluded()
    {
        var ctx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 100m, BoundingBoxZ = 10m },
            1.0m, 0m, 0m, 0m, null, new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        Assert.True(result.MaterialCost > 100m); // 1.15× overhead
    }
}

// ── MJF ───────────────────────────────────────────────────────────────────────

public class MjfPricingCalculatorTests
{
    private readonly MjfPricingCalculator _calculator = new();

    [Fact]
    public void TechnologyName_IsMjf()
        => Assert.Equal("MJF", _calculator.TechnologyName);

    [Fact]
    public void Calculate_ValidInputs_BreakdownIsConsistent()
    {
        var ctx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 50m, BoundingBoxZ = 5m },
            0.9m, 150m, 200m, 600m, null, new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        Assert.True(result.SubtotalBeforeMargin > 0m);
        Assert.Equal(600m, result.MinimumOrderPriceFloor);
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
    }

    [Fact]
    public void Calculate_ZeroVolume_PreservesFloor()
    {
        var ctx = new PricingContext(
            new GeometryMetrics(),
            0.9m, 150m, 0m, 600m, null, new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        Assert.Equal(600m, result.MinimumOrderPriceFloor);
    }

    [Fact]
    public void Calculate_FusingAgentOverheadIncluded()
    {
        var ctx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 100m, BoundingBoxZ = 10m },
            1.0m, 0m, 0m, 0m, null, new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        Assert.True(result.MaterialCost > 100m); // 1.20× overhead
    }
}

// ── Material Jetting ──────────────────────────────────────────────────────────

public class MjPricingCalculatorTests
{
    private readonly MjPricingCalculator _calculator = new();

    [Fact]
    public void TechnologyName_IsMj()
        => Assert.Equal("MJ", _calculator.TechnologyName);

    [Fact]
    public void Calculate_ValidInputs_BreakdownIsConsistent()
    {
        var ctx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 50m, SupportVolumeCm3 = 5m, BoundingBoxZ = 5m },
            2.0m, 200m, 300m, 1000m, null, new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        Assert.True(result.SubtotalBeforeMargin > 0m);
        Assert.Equal(1000m, result.MinimumOrderPriceFloor);
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
    }

    [Fact]
    public void Calculate_WithSupportVolume_IncreasesSupportCost()
    {
        var withoutCtx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 50m, BoundingBoxZ = 5m },
            2.0m, 200m, 0m, 0m, null, new Dictionary<string, string>());
        var withCtx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 50m, SupportVolumeCm3 = 10m, BoundingBoxZ = 5m },
            2.0m, 200m, 0m, 0m, null, new Dictionary<string, string>());

        var without = _calculator.Calculate(withoutCtx);
        var with = _calculator.Calculate(withCtx);

        Assert.True(with.SupportMaterialCost > without.SupportMaterialCost);
        Assert.True(with.SubtotalBeforeMargin > without.SubtotalBeforeMargin);
    }

    [Fact]
    public void Calculate_MoreLayers_HigherMachineCost()
    {
        var short_ = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 50m, BoundingBoxZ = 5m },
            2.0m, 200m, 0m, 0m, null, new Dictionary<string, string>());
        var tall = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 50m, BoundingBoxZ = 50m },
            2.0m, 200m, 0m, 0m, null, new Dictionary<string, string>());

        Assert.True(_calculator.Calculate(tall).MachineTimeCost > _calculator.Calculate(short_).MachineTimeCost);
    }
}

// ── Binder Jetting ────────────────────────────────────────────────────────────

public class BjPricingCalculatorTests
{
    private readonly BjPricingCalculator _calculator = new();

    [Fact]
    public void TechnologyName_IsBj()
        => Assert.Equal("BJ", _calculator.TechnologyName);

    [Fact]
    public void Calculate_ValidInputs_BreakdownIsConsistent()
    {
        var ctx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 50m, BoundingBoxZ = 5m },
            2.0m, 200m, 500m, 2000m, null, new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        Assert.True(result.SubtotalBeforeMargin > 0m);
        Assert.Equal(2000m, result.MinimumOrderPriceFloor);
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
    }

    [Fact]
    public void Calculate_IncludesSinteringSurcharge()
    {
        var ctx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 50m, BoundingBoxZ = 5m },
            2.0m, 200m, 0m, 0m, null, new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        // Sintering adds 35% of (material + base machine); machine time cost > base printing cost
        Assert.True(result.MachineTimeCost > result.MaterialCost * 0.3m);
    }

    [Fact]
    public void Calculate_ZeroVolume_PreservesFloor()
    {
        var ctx = new PricingContext(
            new GeometryMetrics(),
            2.0m, 200m, 0m, 2000m, null, new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        Assert.Equal(2000m, result.MinimumOrderPriceFloor);
    }
}

// ── DMLS ─────────────────────────────────────────────────────────────────────

public class DmlsPricingCalculatorTests
{
    private readonly DmlsPricingCalculator _calculator = new();

    [Fact]
    public void TechnologyName_IsDmls()
        => Assert.Equal("DMLS", _calculator.TechnologyName);

    [Fact]
    public void Calculate_ValidInputs_BreakdownIsConsistent()
    {
        var ctx = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 20m, SupportVolumeCm3 = 5m, BoundingBoxZ = 10m },
            15m, 1000m, 2000m, 10000m, null, new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        Assert.True(result.SubtotalBeforeMargin > 0m);
        Assert.Equal(10000m, result.MinimumOrderPriceFloor);
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
    }

    [Fact]
    public void Calculate_ZeroVolume_PreservesFloor()
    {
        var ctx = new PricingContext(
            new GeometryMetrics(),
            15m, 1000m, 0m, 10000m, null, new Dictionary<string, string>());

        var result = _calculator.Calculate(ctx);

        Assert.Equal(10000m, result.MinimumOrderPriceFloor);
    }

    [Fact]
    public void Calculate_WithSupportRequired_AppliesDfmSurcharge()
    {
        var makeCtx = (bool supportRequired) => new PricingContext(
            new GeometryMetrics { VolumeCm3 = 20m, SupportVolumeCm3 = 5m, BoundingBoxZ = 10m },
            15m, 1000m, 0m, 0m,
            new DfmMetrics { SupportRequired = supportRequired },
            new Dictionary<string, string>());

        var without = _calculator.Calculate(makeCtx(false));
        var with = _calculator.Calculate(makeCtx(true));

        Assert.True(with.DfmSurcharge > without.DfmSurcharge);
    }

    [Fact]
    public void Calculate_SupportVolumeIncludedInMaterialCost()
    {
        var withoutSupport = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 20m, BoundingBoxZ = 10m },
            15m, 0m, 0m, 0m, null, new Dictionary<string, string>());
        var withSupport = new PricingContext(
            new GeometryMetrics { VolumeCm3 = 20m, SupportVolumeCm3 = 10m, BoundingBoxZ = 10m },
            15m, 0m, 0m, 0m, null, new Dictionary<string, string>());

        var wo = _calculator.Calculate(withoutSupport);
        var w = _calculator.Calculate(withSupport);

        Assert.True(w.SupportMaterialCost > 0m);
        Assert.True(w.SubtotalBeforeMargin > wo.SubtotalBeforeMargin);
    }

    [Fact]
    public void Calculate_TallerPart_CostsMoreDueToLayerCount()
    {
        var makeCtx = (decimal z) => new PricingContext(
            new GeometryMetrics { VolumeCm3 = 20m, BoundingBoxZ = z },
            15m, 1000m, 0m, 0m, null, new Dictionary<string, string>());

        var shortPart = _calculator.Calculate(makeCtx(10m));
        var tallPart = _calculator.Calculate(makeCtx(100m));

        Assert.True(tallPart.MachineTimeCost > shortPart.MachineTimeCost);
    }
}

// ── Engine dispatch for new processes ─────────────────────────────────────────

public class RuleBasedPricingEngineNewProcessesTests
{
    private static (RuleBasedPricingEngine engine, PricingConfiguration config) CreateEngine()
    {
        var calculators = new IPricingCalculator[]
        {
            new FdmPricingCalculator(), new SlaPricingCalculator(),
            new CncPricingCalculator(), new CncMillPricingCalculator(),
            new CncTurnPricingCalculator(), new SlsPricingCalculator(),
            new MjfPricingCalculator(), new MjPricingCalculator(),
            new BjPricingCalculator(), new DmlsPricingCalculator(),
            new ScanningPricingCalculator(), new DesignPricingCalculator(),
        };
        var registry = new PricingCalculatorRegistry(calculators);
        var engine = new RuleBasedPricingEngine(
            new Mock<ILogger<RuleBasedPricingEngine>>().Object, registry);
        var config = new PricingConfiguration
        {
            Id = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            ManufacturingProcessId = Guid.NewGuid(),
            MaterialPricePerCm3 = 5.0m,
            MachineHourlyRate = 500m,
            SetupCostFlat = 500m,
            MinimumOrderPrice = 2500m,
            MarginMultiplier = 1.5m,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow,
        };
        return (engine, config);
    }

    private static PricingRequest MakeRequest(string processName) => new()
    {
        FileId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        MaterialId = Guid.NewGuid(),
        MaterialCode = "PA12",
        ManufacturingProcessId = Guid.NewGuid(),
        ManufacturingProcessName = processName,
        Quantity = 1,
        Geometry = new GeometryMetrics
        {
            VolumeCm3 = 50m, SupportVolumeCm3 = 0m, SurfaceAreaCm2 = 100m,
            BoundingBoxX = 10m, BoundingBoxY = 10m, BoundingBoxZ = 5m,
        },
    };

    [Theory]
    [InlineData("CNC_MILL",                       "CNC_MILL")]
    [InlineData("CNC Milling",                    "CNC_MILL")]
    [InlineData("CNC_TURN",                       "CNC_TURN")]
    [InlineData("CNC Turning",                    "CNC_TURN")]
    [InlineData("SLS",                            "SLS")]
    [InlineData("3D Printing (SLS)",              "SLS")]
    [InlineData("MJF",                            "MJF")]
    [InlineData("3D Printing (MJF)",              "MJF")]
    [InlineData("MJ",                             "MJ")]
    [InlineData("3D Printing (Material Jetting)", "MJ")]
    [InlineData("BJ",                             "BJ")]
    [InlineData("3D Printing (Binder Jetting)",   "BJ")]
    [InlineData("DMLS",                           "DMLS")]
    [InlineData("3D Printing (DMLS)",             "DMLS")]
    public async Task CalculateAsync_NewProcess_RoutesToCorrectCalculator(
        string processName, string expectedEngineToken)
    {
        var (engine, config) = CreateEngine();

        var result = await engine.CalculateAsync(MakeRequest(processName), config, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Breakdown.SubtotalBeforeMargin >= 0m);
        Assert.Contains(expectedEngineToken, result.EngineName, StringComparison.OrdinalIgnoreCase);
    }
}
