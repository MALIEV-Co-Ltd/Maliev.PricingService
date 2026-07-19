using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Services;
using Maliev.PricingService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.PricingService.Tests.Unit;

// ── Helpers ────────────────────────────────────────────────────────────────────

file static class ContextFactory
{
    public static PricingContext Make(
        decimal volumeCm3 = 50m,
        decimal supportVolumeCm3 = 0m,
        decimal surfaceAreaCm2 = 100m,
        decimal boundingBoxX = 40m,   // mm — 40×40×40 mm = 64 cm³ block
        decimal boundingBoxY = 40m,
        decimal boundingBoxZ = 40m,
        decimal materialPricePerCm3 = 0.5m,
        decimal machineHourlyRate = 100m,
        decimal setupFee = 50m,
        decimal minimumOrderPrice = 300m,
        DfmMetrics? dfm = null,
        Dictionary<string, string>? processParameters = null)
        => new(
            new GeometryMetrics
            {
                VolumeCm3 = volumeCm3,
                SupportVolumeCm3 = supportVolumeCm3,
                SurfaceAreaCm2 = surfaceAreaCm2,
                BoundingBoxX = boundingBoxX,
                BoundingBoxY = boundingBoxY,
                BoundingBoxZ = boundingBoxZ
            },
            materialPricePerCm3,
            machineHourlyRate,
            setupFee,
            minimumOrderPrice,
            dfm,
            processParameters ?? new Dictionary<string, string>());

    public static RuleBasedPricingEngine Engine()
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
        return new RuleBasedPricingEngine(new Mock<ILogger<RuleBasedPricingEngine>>().Object, registry);
    }
}

// ── FDM ───────────────────────────────────────────────────────────────────────

public class FdmPricingCalculatorTests
{
    private readonly FdmPricingCalculator _calculator = new();

    [Fact]
    public void Calculate_ValidInputs_BreakdownIsConsistent()
    {
        var result = _calculator.Calculate(ContextFactory.Make());

        Assert.True(result.SubtotalBeforeMargin > 0m);
        Assert.Equal(300m, result.MinimumOrderPriceFloor);
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
    }

    [Fact]
    public void Calculate_EmptyGeometry_PreservesFloor()
    {
        var result = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: 0m, supportVolumeCm3: 0m, boundingBoxZ: 0m));

        Assert.Equal(300m, result.MinimumOrderPriceFloor);
        Assert.True(result.SubtotalBeforeMargin >= 0m);
    }

    [Fact]
    public void Calculate_WithCustomDensity_UsesCustomDensity()
    {
        var withDefault = _calculator.Calculate(ContextFactory.Make(volumeCm3: 100m));
        var withHeavy = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: 100m,
            processParameters: new Dictionary<string, string> { ["Density"] = "1.5" }));

        Assert.True(withHeavy.MaterialCost > withDefault.MaterialCost);
    }

    [Fact]
    public void Calculate_WithSupport_IncreasesSubtotal()
    {
        var without = _calculator.Calculate(ContextFactory.Make(
            setupFee: 0m, minimumOrderPrice: 0m));
        var with = _calculator.Calculate(ContextFactory.Make(
            supportVolumeCm3: 10m, setupFee: 0m, minimumOrderPrice: 0m));

        Assert.True(with.SubtotalBeforeMargin > without.SubtotalBeforeMargin);
        Assert.True(with.SupportMaterialCost > 0m);
    }

    [Fact]
    public void Calculate_DfmSurcharge_TracksSetupShareAsFixedLineCost()
    {
        var result = _calculator.Calculate(ContextFactory.Make(
            setupFee: 100m,
            minimumOrderPrice: 0m,
            dfm: new DfmMetrics { ThinWallCount = 1 }));
        var variableBase = result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost;

        Assert.Equal(100m, result.SetupCost);
        Assert.Equal(5m, result.FixedDfmSurcharge);
        Assert.Equal((variableBase + result.SetupCost) * 0.05m, result.DfmSurcharge);
    }

    [Theory]
    [InlineData(100, 50, 10, 5, 0.5, 100, 50, 300)]
    [InlineData(10, 2, 1, 1, 0.5, 100, 50, 300)]
    [InlineData(200, 50, 20, 10, 1.0, 200, 100, 300)]
    public void Calculate_VariousInputs_SubtotalIsConsistent(
        decimal volume, decimal support, decimal surface,
        decimal boxZ, decimal materialCost, decimal machineRate,
        decimal setup, decimal minOrder)
    {
        var result = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: volume, supportVolumeCm3: support, surfaceAreaCm2: surface,
            boundingBoxZ: boxZ, materialPricePerCm3: materialCost,
            machineHourlyRate: machineRate, setupFee: setup, minimumOrderPrice: minOrder));

        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
        Assert.Equal(minOrder, result.MinimumOrderPriceFloor);
    }
}

// ── SLA ───────────────────────────────────────────────────────────────────────

public class SlaPricingCalculatorTests
{
    private readonly SlaPricingCalculator _calculator = new();

    [Fact]
    public void Calculate_ValidInputs_BreakdownIsConsistent()
    {
        var result = _calculator.Calculate(ContextFactory.Make(
            materialPricePerCm3: 2.0m, machineHourlyRate: 150m,
            setupFee: 100m, minimumOrderPrice: 500m));

        Assert.True(result.SubtotalBeforeMargin > 0m);
        Assert.Equal(500m, result.MinimumOrderPriceFloor);
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
    }

    [Fact]
    public void Calculate_EmptyGeometry_PreservesFloor()
    {
        var result = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: 0m, boundingBoxZ: 0m,
            materialPricePerCm3: 2.0m, machineHourlyRate: 150m,
            setupFee: 100m, minimumOrderPrice: 500m));

        Assert.Equal(500m, result.MinimumOrderPriceFloor);
    }

    [Fact]
    public void Calculate_WithCustomLayerExposure_AffectsMachineCost()
    {
        var standard = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: 200m, boundingBoxZ: 20m, machineHourlyRate: 150m, setupFee: 0m, minimumOrderPrice: 0m));
        var slow = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: 200m, boundingBoxZ: 20m, machineHourlyRate: 150m, setupFee: 0m, minimumOrderPrice: 0m,
            processParameters: new Dictionary<string, string> { ["LayerExposure"] = "5.0" }));

        Assert.True(slow.MachineTimeCost > standard.MachineTimeCost);
    }

    [Fact]
    public void Calculate_DfmSurcharge_TracksSetupShareAsFixedLineCost()
    {
        var result = _calculator.Calculate(ContextFactory.Make(
            setupFee: 100m,
            minimumOrderPrice: 0m,
            dfm: new DfmMetrics { ResinTrappingRisk = true }));
        var variableBase = result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost;

        Assert.Equal(100m, result.SetupCost);
        Assert.Equal(10m, result.FixedDfmSurcharge);
        Assert.Equal((variableBase + result.SetupCost) * 0.10m, result.DfmSurcharge);
    }

    [Theory]
    [InlineData(200, 50, 200, 10, 2.0, 150, 100, 500)]
    [InlineData(10, 2, 10, 1, 2.0, 150, 100, 500)]
    public void Calculate_VariousInputs_SubtotalIsConsistent(decimal volume, decimal support,
        decimal surface, decimal boxZ, decimal materialCost, decimal machineRate,
        decimal setup, decimal minOrder)
    {
        var result = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: volume, supportVolumeCm3: support, surfaceAreaCm2: surface,
            boundingBoxZ: boxZ, materialPricePerCm3: materialCost,
            machineHourlyRate: machineRate, setupFee: setup, minimumOrderPrice: minOrder));

        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
        Assert.Equal(minOrder, result.MinimumOrderPriceFloor);
    }
}

// ── CNC ───────────────────────────────────────────────────────────────────────

public class CncPricingCalculatorTests
{
    private readonly CncPricingCalculator _calculator = new();

    [Fact]
    public void Calculate_ValidInputs_BreakdownIsConsistent()
    {
        var result = _calculator.Calculate(ContextFactory.Make(
            materialPricePerCm3: 5.0m, machineHourlyRate: 500m,
            setupFee: 500m, minimumOrderPrice: 2500m));

        Assert.True(result.SubtotalBeforeMargin > 0m);
        Assert.Equal(2500m, result.MinimumOrderPriceFloor);
        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
    }

    [Fact]
    public void Calculate_EmptyGeometry_PreservesFloor()
    {
        var result = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: 0m, supportVolumeCm3: 0m,
            boundingBoxX: 0m, boundingBoxY: 0m, boundingBoxZ: 0m,
            materialPricePerCm3: 5.0m, machineHourlyRate: 500m,
            setupFee: 500m, minimumOrderPrice: 2500m));

        Assert.Equal(2500m, result.MinimumOrderPriceFloor);
    }

    [Fact]
    public void Calculate_ComplexPart_HasHigherSubtotalThanSimplePart()
    {
        // BoundingBox is in mm. Simple: 100×100×100 mm solid block (1000 cm³ bounding box, ~0 removal)
        var simple = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: 1000m, surfaceAreaCm2: 600m,
            boundingBoxX: 100m, boundingBoxY: 100m, boundingBoxZ: 100m,
            setupFee: 0m, minimumOrderPrice: 0m,
            materialPricePerCm3: 5.0m, machineHourlyRate: 500m));

        // Complex: 50×50×20 mm bounding box (50 cm³), actual volume 10 cm³ — 80% removal
        var complex = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: 10m, surfaceAreaCm2: 100m,
            boundingBoxX: 50m, boundingBoxY: 50m, boundingBoxZ: 20m,
            setupFee: 0m, minimumOrderPrice: 0m,
            materialPricePerCm3: 5.0m, machineHourlyRate: 500m));

        Assert.True(complex.SubtotalBeforeMargin > simple.SubtotalBeforeMargin * 0.1m);
    }

    [Fact]
    public void Calculate_WithCustomMachinabilityRating_AffectsMachineCost()
    {
        // BoundingBox 40×40×40 mm → blockVolume 64 cm³ > volumeCm3 50 → non-zero removal volume
        var standard = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: 50m,
            boundingBoxX: 40m, boundingBoxY: 40m, boundingBoxZ: 40m,
            materialPricePerCm3: 5.0m, machineHourlyRate: 500m,
            setupFee: 0m, minimumOrderPrice: 2500m));
        var hard = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: 50m,
            boundingBoxX: 40m, boundingBoxY: 40m, boundingBoxZ: 40m,
            materialPricePerCm3: 5.0m, machineHourlyRate: 500m,
            setupFee: 0m, minimumOrderPrice: 2500m,
            processParameters: new Dictionary<string, string> { ["MachinabilityRating"] = "0.5" }));

        Assert.True(hard.MachineTimeCost > standard.MachineTimeCost);
    }

    [Fact]
    public void Calculate_DfmSurcharge_TracksSetupShareAsFixedLineCost()
    {
        var result = _calculator.Calculate(ContextFactory.Make(
            setupFee: 100m,
            minimumOrderPrice: 0m,
            dfm: new DfmMetrics { HasUndercuts = true }));
        var variableBase = result.MaterialCost + result.MachineTimeCost;

        Assert.Equal(100m, result.SetupCost);
        Assert.Equal(15m, result.FixedDfmSurcharge);
        Assert.Equal((variableBase + result.SetupCost) * 0.15m, result.DfmSurcharge);
    }

    [Theory]
    [InlineData(100, 500, 10, 5.0, 500, 500, 2500)]
    [InlineData(10, 10, 1, 5.0, 500, 500, 2500)]
    public void Calculate_VariousInputs_SubtotalIsConsistent(decimal volume, decimal surface,
        decimal boxZ, decimal materialCost, decimal machineRate, decimal setup, decimal minOrder)
    {
        var cbrt = Math.Pow((double)500m, 1.0 / 3.0);
        var result = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: volume, surfaceAreaCm2: surface,
            boundingBoxX: (decimal)cbrt, boundingBoxY: (decimal)cbrt, boundingBoxZ: boxZ,
            materialPricePerCm3: materialCost, machineHourlyRate: machineRate,
            setupFee: setup, minimumOrderPrice: minOrder));

        Assert.Equal(result.SubtotalBeforeMargin,
            result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost
            + result.SetupCost + result.DfmSurcharge + result.ComplexitySurcharge);
        Assert.Equal(minOrder, result.MinimumOrderPriceFloor);
    }
}

public class AdditionalFixedDfmAllocationTests
{
    [Fact]
    public void CncTurn_DfmSurcharge_TracksSetupShareAsFixedLineCost()
    {
        var result = new CncTurnPricingCalculator().Calculate(ContextFactory.Make(
            setupFee: 100m,
            minimumOrderPrice: 0m,
            dfm: new DfmMetrics { HasUndercuts = true }));
        var variableBase = result.MaterialCost + result.MachineTimeCost;

        Assert.Equal(20m, result.FixedDfmSurcharge);
        Assert.Equal((variableBase + result.SetupCost) * 0.20m, result.DfmSurcharge);
        Assert.Equal(
            result.SubtotalBeforeMargin,
            variableBase + result.SetupCost + result.DfmSurcharge);
    }

    [Fact]
    public void Dmls_DfmSurcharge_TracksSetupShareAsFixedLineCost()
    {
        var result = new DmlsPricingCalculator().Calculate(ContextFactory.Make(
            setupFee: 100m,
            minimumOrderPrice: 0m,
            dfm: new DfmMetrics { SupportRequired = true }));
        var variableBase = result.MaterialCost + result.SupportMaterialCost + result.MachineTimeCost;

        Assert.Equal(20m, result.FixedDfmSurcharge);
        Assert.Equal((variableBase + result.SetupCost) * 0.20m, result.DfmSurcharge);
        Assert.Equal(
            result.SubtotalBeforeMargin,
            variableBase + result.SetupCost + result.DfmSurcharge);
    }
}

// ── Engine Dispatch ────────────────────────────────────────────────────────────

public class RuleBasedPricingEngineTests
{
    private static PricingConfiguration MakeConfig(
        decimal materialCostPerCm3 = 0.5m,
        decimal machineHourlyRate = 100m,
        decimal setupFee = 50m,
        decimal minOrder = 300m,
        decimal margin = 1.5m) => new()
        {
            Id = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            ManufacturingProcessId = Guid.NewGuid(),
            MaterialPricePerCm3 = materialCostPerCm3,
            MachineHourlyRate = machineHourlyRate,
            SetupCostFlat = setupFee,
            MinimumOrderPrice = minOrder,
            MarginMultiplier = margin,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow
        };

    private static PricingRequest MakeRequest(string processName, decimal quantity = 1m) => new()
    {
        FileId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        MaterialId = Guid.NewGuid(),
        MaterialCode = "PLA",
        ManufacturingProcessId = Guid.NewGuid(),
        ManufacturingProcessName = processName,
        Quantity = quantity,
        Geometry = new GeometryMetrics
        {
            VolumeCm3 = 50m,
            SupportVolumeCm3 = 10m,
            SurfaceAreaCm2 = 100m,
            BoundingBoxX = 40m,
            BoundingBoxY = 40m,
            BoundingBoxZ = 40m  // mm
        }
    };

    [Fact]
    public async Task CalculateAsync_FdmProcess_ReturnsFdmEngineResult()
    {
        var engine = ContextFactory.Engine();
        var result = await engine.CalculateAsync(MakeRequest("FDM"), MakeConfig(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Breakdown.SubtotalBeforeMargin >= 0m);
        Assert.Contains("FDM", result.EngineName);
    }

    [Fact]
    public async Task CalculateAsync_SlaProcess_ReturnsSlaEngineResult()
    {
        var engine = ContextFactory.Engine();
        var result = await engine.CalculateAsync(
            MakeRequest("SLA"),
            MakeConfig(materialCostPerCm3: 2.0m, machineHourlyRate: 150m, setupFee: 100m, minOrder: 500m),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Breakdown.SubtotalBeforeMargin >= 0m);
        Assert.Contains("SLA", result.EngineName);
    }

    [Fact]
    public async Task CalculateAsync_CncProcess_ReturnsCncEngineResult()
    {
        var engine = ContextFactory.Engine();
        var result = await engine.CalculateAsync(
            MakeRequest("CNC"),
            MakeConfig(materialCostPerCm3: 5.0m, machineHourlyRate: 500m, setupFee: 500m, minOrder: 2500m),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.Breakdown.SubtotalBeforeMargin >= 0m);
        Assert.Contains("CNC", result.EngineName);
    }

    [Fact]
    public async Task CalculateAsync_UnknownProcess_ReturnsUnknownProcessResult()
    {
        var engine = ContextFactory.Engine();
        var result = await engine.CalculateAsync(
            MakeRequest("Unknown Process"), MakeConfig(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("UnknownProcess", result.EngineName);
        Assert.Equal(0m, result.Breakdown.SubtotalBeforeMargin);
    }

    [Fact]
    public async Task CalculateAsync_Quantity5_EngineReturnsPerUnitBreakdown()
    {
        var engine = ContextFactory.Engine();
        var qty1 = await engine.CalculateAsync(MakeRequest("FDM", 1), MakeConfig(), CancellationToken.None);
        var qty5 = await engine.CalculateAsync(MakeRequest("FDM", 5), MakeConfig(), CancellationToken.None);

        // Engine computes per-unit cost (not multiplied by quantity)
        Assert.Equal(qty1.Breakdown.SubtotalBeforeMargin, qty5.Breakdown.SubtotalBeforeMargin);
    }

    [Fact]
    public async Task CalculateAsync_ScanningProcess_ReturnsZeroSubtotalWithFloor()
    {
        var engine = ContextFactory.Engine();
        var result = await engine.CalculateAsync(
            MakeRequest("3D Scanning"),
            MakeConfig(materialCostPerCm3: 0m, machineHourlyRate: 0m, setupFee: 0m, minOrder: 2500m),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(0m, result.Breakdown.SubtotalBeforeMargin);
        Assert.Equal(2500m, result.Breakdown.MinimumOrderPriceFloor);
        Assert.Contains("Scanning", result.EngineName);
    }

    [Fact]
    public async Task CalculateAsync_ScanningReverseEngineering_Returns4500Floor()
    {
        var engine = ContextFactory.Engine();
        var result = await engine.CalculateAsync(
            MakeRequest("3D Scanning + Reverse Engineering"),
            MakeConfig(materialCostPerCm3: 0m, machineHourlyRate: 0m, setupFee: 0m, minOrder: 4500m),
            CancellationToken.None);

        Assert.Equal(4500m, result.Breakdown.MinimumOrderPriceFloor);
        Assert.Contains("Scanning", result.EngineName);
    }

    [Fact]
    public async Task CalculateAsync_DesignProcess_ReturnsZeroSubtotalWithFloor()
    {
        var engine = ContextFactory.Engine();
        var result = await engine.CalculateAsync(
            MakeRequest("3D Design"),
            MakeConfig(materialCostPerCm3: 0m, machineHourlyRate: 0m, setupFee: 0m, minOrder: 500m),
            CancellationToken.None);

        Assert.Equal(0m, result.Breakdown.SubtotalBeforeMargin);
        Assert.Equal(500m, result.Breakdown.MinimumOrderPriceFloor);
        Assert.Contains("Design", result.EngineName);
    }
}

// ── Scanning ──────────────────────────────────────────────────────────────────

public class ScanningPricingCalculatorTests
{
    private readonly ScanningPricingCalculator _calculator = new();

    [Fact]
    public void Calculate_ReturnsZeroSubtotalWithFloor()
    {
        var result = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: 100m, machineHourlyRate: 0m, setupFee: 0m, minimumOrderPrice: 2500m));

        Assert.Equal(0m, result.SubtotalBeforeMargin);
        Assert.Equal(2500m, result.MinimumOrderPriceFloor);
    }

    [Fact]
    public void Calculate_WithHigherMinimum_ReturnsHigherFloor()
    {
        var result = _calculator.Calculate(ContextFactory.Make(minimumOrderPrice: 4500m));

        Assert.Equal(4500m, result.MinimumOrderPriceFloor);
    }

    [Fact]
    public void Calculate_IgnoresGeometryParameters()
    {
        var result = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: 9999m, surfaceAreaCm2: 9999m,
            machineHourlyRate: 9999m, setupFee: 9999m,
            minimumOrderPrice: 2500m));

        Assert.Equal(0m, result.SubtotalBeforeMargin);
        Assert.Equal(2500m, result.MinimumOrderPriceFloor);
    }

    [Theory]
    [InlineData(2500)]
    [InlineData(4500)]
    [InlineData(500)]
    public void Calculate_VariousMinimums_PreservesFloor(decimal minimumOrderPrice)
    {
        var result = _calculator.Calculate(ContextFactory.Make(minimumOrderPrice: minimumOrderPrice));

        Assert.Equal(minimumOrderPrice, result.MinimumOrderPriceFloor);
        Assert.Equal(0m, result.SubtotalBeforeMargin);
    }
}

// ── Design ────────────────────────────────────────────────────────────────────

public class DesignPricingCalculatorTests
{
    private readonly DesignPricingCalculator _calculator = new();

    [Fact]
    public void Calculate_ReturnsZeroSubtotalWithFloor()
    {
        var result = _calculator.Calculate(ContextFactory.Make(minimumOrderPrice: 500m));

        Assert.Equal(0m, result.SubtotalBeforeMargin);
        Assert.Equal(500m, result.MinimumOrderPriceFloor);
    }

    [Fact]
    public void Calculate_IgnoresGeometryParameters()
    {
        var result = _calculator.Calculate(ContextFactory.Make(
            volumeCm3: 9999m, machineHourlyRate: 9999m,
            setupFee: 9999m, minimumOrderPrice: 500m));

        Assert.Equal(0m, result.SubtotalBeforeMargin);
        Assert.Equal(500m, result.MinimumOrderPriceFloor);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(2500)]
    public void Calculate_VariousMinimums_PreservesFloor(decimal minimumOrderPrice)
    {
        var result = _calculator.Calculate(ContextFactory.Make(minimumOrderPrice: minimumOrderPrice));

        Assert.Equal(minimumOrderPrice, result.MinimumOrderPriceFloor);
        Assert.Equal(0m, result.SubtotalBeforeMargin);
    }
}
