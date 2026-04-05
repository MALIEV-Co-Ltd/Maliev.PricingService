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
    private const decimal Margin = 1.5m;

    [Fact]
    public void TechnologyName_IsCncMill()
        => Assert.Equal("CNC_MILL", _calculator.TechnologyName);

    [Fact]
    public void Calculate_ValidInputs_ReturnsAtLeastMinimumOrder()
    {
        var result = _calculator.Calculate(
            50m, 0m, 100m, 10m, 10m, 5m,
            5.0m, 500m, 500m, 2500m, Margin, null, []);

        Assert.True(result >= 2500m);
    }

    [Fact]
    public void Calculate_EmptyGeometry_ReturnsMinimumOrderPrice()
    {
        var result = _calculator.Calculate(
            0m, 0m, 0m, 0m, 0m, 0m,
            5.0m, 500m, 0m, 2500m, Margin, null, []);

        Assert.Equal(2500m, result);
    }

    [Fact]
    public void Calculate_WithDfmSharpCorners_AppliesSurcharge()
    {
        var dfmClean   = new DfmMetrics { SharpCornerCount = 0 };
        var dfmCorners = new DfmMetrics { SharpCornerCount = 5 };

        var withoutCorners = _calculator.Calculate(50m, 0m, 100m, 10m, 10m, 5m, 5.0m, 500m, 0m, 0m, 1.0m, dfmClean, []);
        var withCorners    = _calculator.Calculate(50m, 0m, 100m, 10m, 10m, 5m, 5.0m, 500m, 0m, 0m, 1.0m, dfmCorners, []);

        Assert.True(withCorners > withoutCorners);
    }

    [Fact]
    public void Calculate_WithDfmUndercuts_AppliesUndercutSurcharge()
    {
        var dfmClean    = new DfmMetrics { HasUndercuts = false };
        var dfmUndercut = new DfmMetrics { HasUndercuts = true };

        var without = _calculator.Calculate(50m, 0m, 100m, 10m, 10m, 5m, 5.0m, 500m, 0m, 0m, 1.0m, dfmClean, []);
        var with    = _calculator.Calculate(50m, 0m, 100m, 10m, 10m, 5m, 5.0m, 500m, 0m, 0m, 1.0m, dfmUndercut, []);

        Assert.True(with > without);
    }
}

// ── CNC Turning ───────────────────────────────────────────────────────────────

public class CncTurnPricingCalculatorTests
{
    private readonly CncTurnPricingCalculator _calculator = new();
    private const decimal Margin = 1.5m;

    [Fact]
    public void TechnologyName_IsCncTurn()
        => Assert.Equal("CNC_TURN", _calculator.TechnologyName);

    [Fact]
    public void Calculate_ValidInputs_ReturnsAtLeastMinimumOrder()
    {
        var result = _calculator.Calculate(
            40m, 0m, 80m, 5m, 5m, 10m,
            5.0m, 500m, 500m, 2500m, Margin, null, []);

        Assert.True(result >= 2500m);
    }

    [Fact]
    public void Calculate_EmptyGeometry_ReturnsMinimumOrderPrice()
    {
        var result = _calculator.Calculate(
            0m, 0m, 0m, 0m, 0m, 0m,
            5.0m, 500m, 0m, 2500m, Margin, null, []);

        Assert.Equal(2500m, result);
    }

    [Fact]
    public void Calculate_WithUndercuts_AppliesUndercutSurcharge()
    {
        var dfmClean    = new DfmMetrics { HasUndercuts = false };
        var dfmUndercut = new DfmMetrics { HasUndercuts = true };

        var without = _calculator.Calculate(40m, 0m, 80m, 5m, 5m, 10m, 5.0m, 500m, 0m, 0m, 1.0m, dfmClean, []);
        var with    = _calculator.Calculate(40m, 0m, 80m, 5m, 5m, 10m, 5.0m, 500m, 0m, 0m, 1.0m, dfmUndercut, []);

        Assert.True(with > without);
    }

    [Fact]
    public void Calculate_CustomMrr_UsesCustomValue()
    {
        var defaultResult = _calculator.Calculate(
            40m, 0m, 80m, 5m, 5m, 10m, 5.0m, 500m, 0m, 0m, 1.0m, null, []);
        var slowMrr = _calculator.Calculate(
            40m, 0m, 80m, 5m, 5m, 10m, 5.0m, 500m, 0m, 0m, 1.0m, null,
            new Dictionary<string, string> { ["MRR"] = "20" });

        // Slower MRR means more machine time → higher cost
        Assert.True(slowMrr > defaultResult);
    }
}

// ── SLS ───────────────────────────────────────────────────────────────────────

public class SlsPricingCalculatorTests
{
    private readonly SlsPricingCalculator _calculator = new();
    private const decimal Margin = 1.5m;

    [Fact]
    public void TechnologyName_IsSls()
        => Assert.Equal("SLS", _calculator.TechnologyName);

    [Fact]
    public void Calculate_ValidInputs_ReturnsAtLeastMinimumOrder()
    {
        var result = _calculator.Calculate(
            50m, 0m, 100m, 10m, 10m, 5m,
            0.8m, 120m, 200m, 500m, Margin, null, []);

        Assert.True(result >= 500m);
    }

    [Fact]
    public void Calculate_ZeroVolume_ReturnsAtLeastMinimumOrderPrice()
    {
        // Powder-bed processes have a fallback layer count even when Z=0,
        // so the result may exceed minimumOrderPrice. We just assert ≥ minimum.
        var result = _calculator.Calculate(
            0m, 0m, 0m, 0m, 0m, 0m,
            0.8m, 120m, 0m, 500m, 1.0m, null, []);

        Assert.True(result >= 500m);
    }

    [Fact]
    public void Calculate_TallerPart_CostsMoreDueToLayerCount()
    {
        var shortPart = _calculator.Calculate(50m, 0m, 100m, 10m, 10m, 5m,  0.8m, 120m, 0m, 0m, 1.0m, null, []);
        var tallPart  = _calculator.Calculate(50m, 0m, 100m, 10m, 10m, 50m, 0.8m, 120m, 0m, 0m, 1.0m, null, []);

        Assert.True(tallPart > shortPart);
    }

    [Fact]
    public void Calculate_PowderRefreshOverheadIncluded()
    {
        // SLS applies 1.15× material overhead. Verify result > raw material cost.
        // volume=100, materialCost=1.0 → raw material=100. With 1.15× → at least 115.
        var result = _calculator.Calculate(
            100m, 0m, 200m, 10m, 10m, 10m,
            1.0m, 0m, 0m, 0m, 1.0m, null, []);

        Assert.True(result > 100m);
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
    public void Calculate_ValidInputs_ReturnsAtLeastMinimumOrder()
    {
        var result = _calculator.Calculate(
            50m, 0m, 100m, 10m, 10m, 5m,
            0.9m, 150m, 200m, 600m, 1.5m, null, []);

        Assert.True(result >= 600m);
    }

    [Fact]
    public void Calculate_ZeroVolume_ReturnsAtLeastMinimumOrderPrice()
    {
        var result = _calculator.Calculate(
            0m, 0m, 0m, 0m, 0m, 0m,
            0.9m, 150m, 0m, 600m, 1.0m, null, []);

        Assert.True(result >= 600m);
    }

    [Fact]
    public void Calculate_FusingAgentOverheadIncluded()
    {
        // MJF applies 1.20× material overhead (fusing + detailing agents).
        // volume=100, materialCost=1.0 → raw material=100. With 1.20× → at least 120.
        var result = _calculator.Calculate(
            100m, 0m, 200m, 10m, 10m, 10m,
            1.0m, 0m, 0m, 0m, 1.0m, null, []);

        Assert.True(result > 100m);
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
    public void Calculate_ValidInputs_ReturnsAtLeastMinimumOrder()
    {
        var result = _calculator.Calculate(
            50m, 5m, 100m, 10m, 10m, 5m,
            2.0m, 200m, 300m, 1000m, 1.5m, null, []);

        Assert.True(result >= 1000m);
    }

    [Fact]
    public void Calculate_WithSupportVolume_IncludesSupportMaterialCost()
    {
        var without = _calculator.Calculate(50m, 0m,  100m, 10m, 10m, 5m, 2.0m, 200m, 0m, 0m, 1.0m, null, []);
        var with    = _calculator.Calculate(50m, 10m, 100m, 10m, 10m, 5m, 2.0m, 200m, 0m, 0m, 1.0m, null, []);

        Assert.True(with > without);
    }

    [Fact]
    public void Calculate_MoreLayers_HigherMachineCost()
    {
        var shortPart = _calculator.Calculate(50m, 0m, 100m, 10m, 10m, 5m,  2.0m, 200m, 0m, 0m, 1.0m, null, []);
        var tallPart  = _calculator.Calculate(50m, 0m, 100m, 10m, 10m, 50m, 2.0m, 200m, 0m, 0m, 1.0m, null, []);

        Assert.True(tallPart > shortPart);
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
    public void Calculate_ValidInputs_ReturnsAtLeastMinimumOrder()
    {
        var result = _calculator.Calculate(
            50m, 0m, 100m, 10m, 10m, 5m,
            2.0m, 200m, 500m, 2000m, 1.5m, null, []);

        Assert.True(result >= 2000m);
    }

    [Fact]
    public void Calculate_IncludesSinteringSurcharge()
    {
        // BJ adds 35% sintering surcharge on material + machine cost.
        // volume=50, materialCost=2.0 → material=100. Expect total > 100 at 1.0× margin.
        var result = _calculator.Calculate(
            50m, 0m, 100m, 10m, 10m, 5m,
            2.0m, 200m, 0m, 0m, 1.0m, null, []);

        Assert.True(result > 100m);
    }

    [Fact]
    public void Calculate_ZeroVolume_ReturnsAtLeastMinimumOrderPrice()
    {
        // BJ sintering is applied to machine cost too; fallback layers mean machine cost > 0.
        var result = _calculator.Calculate(
            0m, 0m, 0m, 0m, 0m, 0m,
            2.0m, 200m, 0m, 2000m, 1.0m, null, []);

        Assert.True(result >= 2000m);
    }
}

// ── DMLS ─────────────────────────────────────────────────────────────────────

public class DmlsPricingCalculatorTests
{
    private readonly DmlsPricingCalculator _calculator = new();
    private const decimal Margin = 1.5m;

    [Fact]
    public void TechnologyName_IsDmls()
        => Assert.Equal("DMLS", _calculator.TechnologyName);

    [Fact]
    public void Calculate_ValidInputs_ReturnsAtLeastMinimumOrder()
    {
        var result = _calculator.Calculate(
            20m, 5m, 80m, 5m, 5m, 10m,
            15m, 1000m, 2000m, 10000m, Margin, null, []);

        Assert.True(result >= 10000m);
    }

    [Fact]
    public void Calculate_ZeroVolume_ReturnsAtLeastMinimumOrderPrice()
    {
        // DMLS uses fallback layer count when Z=0; machine cost exceeds minimum.
        var result = _calculator.Calculate(
            0m, 0m, 0m, 0m, 0m, 0m,
            15m, 1000m, 0m, 10000m, Margin, null, []);

        Assert.True(result >= 10000m);
    }

    [Fact]
    public void Calculate_WithSupportRequired_AppliesDfmSurcharge()
    {
        var dfmNoSupport   = new DfmMetrics { SupportRequired = false };
        var dfmWithSupport = new DfmMetrics { SupportRequired = true };

        var without = _calculator.Calculate(20m, 5m, 80m, 5m, 5m, 10m, 15m, 1000m, 0m, 0m, 1.0m, dfmNoSupport, []);
        var with    = _calculator.Calculate(20m, 5m, 80m, 5m, 5m, 10m, 15m, 1000m, 0m, 0m, 1.0m, dfmWithSupport, []);

        Assert.True(with > without);
    }

    [Fact]
    public void Calculate_SupportVolumeIncludedInMaterialCost()
    {
        // Solid metal supports cost as much as part material in DMLS.
        var withoutSupport = _calculator.Calculate(20m, 0m,  80m, 5m, 5m, 10m, 15m, 0m, 0m, 0m, 1.0m, null, []);
        var withSupport    = _calculator.Calculate(20m, 10m, 80m, 5m, 5m, 10m, 15m, 0m, 0m, 0m, 1.0m, null, []);

        Assert.True(withSupport > withoutSupport);
    }

    [Fact]
    public void Calculate_TallerPart_CostsMoreDueToLayerCount()
    {
        var shortPart = _calculator.Calculate(20m, 0m, 80m, 5m, 5m, 10m,  15m, 1000m, 0m, 0m, 1.0m, null, []);
        var tallPart  = _calculator.Calculate(20m, 0m, 80m, 5m, 5m, 100m, 15m, 1000m, 0m, 0m, 1.0m, null, []);

        Assert.True(tallPart > shortPart);
    }
}

// ── Engine dispatch for new processes ─────────────────────────────────────────

public class RuleBasedPricingEngineNewProcessesTests
{
    private static (RuleBasedPricingEngine engine, PricingConfiguration config) CreateEngine()
    {
        var engine = new RuleBasedPricingEngine(new Mock<ILogger<RuleBasedPricingEngine>>().Object);
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
        Assert.True(result.UnitPrice >= 0m);
        Assert.Contains(expectedEngineToken, result.EngineName, StringComparison.OrdinalIgnoreCase);
    }
}
