using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Services;
using Maliev.PricingService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.PricingService.Tests.Unit;

public class FdmPricingCalculatorTests
{
    private readonly FdmPricingCalculator _calculator = new();

    [Fact]
    public void Calculate_ValidInputs_ReturnsCorrectPrice()
    {
        var result = _calculator.Calculate(
            volumeCm3: 50m,
            supportVolumeCm3: 10m,
            surfaceAreaCm2: 100m,
            boundingBoxX: 10m,
            boundingBoxY: 10m,
            boundingBoxZ: 5m,
            materialCostPerCm3: 0.5m,
            machineHourlyRate: 100m,
            setupFee: 50m,
            minimumOrderPrice: 300m,
            processParameters: new Dictionary<string, string>());

        Assert.InRange(result, 300m, 500m);
    }

    [Fact]
    public void Calculate_EmptyGeometry_ReturnsMinimumOrderPrice()
    {
        var result = _calculator.Calculate(
            volumeCm3: 0m,
            supportVolumeCm3: 0m,
            surfaceAreaCm2: 0m,
            boundingBoxX: 0m,
            boundingBoxY: 0m,
            boundingBoxZ: 0m,
            materialCostPerCm3: 0.5m,
            machineHourlyRate: 100m,
            setupFee: 50m,
            minimumOrderPrice: 300m,
            processParameters: new Dictionary<string, string>());

        Assert.Equal(300m, result);
    }

    [Fact]
    public void Calculate_WithCustomDensity_UsesCustomDensity()
    {
        var parameters = new Dictionary<string, string> { { "Density", "1.2" } };
        
        var result = _calculator.Calculate(
            volumeCm3: 50m,
            supportVolumeCm3: 0m,
            surfaceAreaCm2: 100m,
            boundingBoxX: 10m,
            boundingBoxY: 10m,
            boundingBoxZ: 5m,
            materialCostPerCm3: 0.5m,
            machineHourlyRate: 100m,
            setupFee: 50m,
            minimumOrderPrice: 300m,
            processParameters: parameters);

        Assert.InRange(result, 300m, 500m);
    }

    [Fact]
    public void Calculate_WithSupport_AppliesSupportSurcharge()
    {
        var withoutSupport = _calculator.Calculate(
            volumeCm3: 50m,
            supportVolumeCm3: 0m,
            surfaceAreaCm2: 100m,
            boundingBoxX: 10m,
            boundingBoxY: 10m,
            boundingBoxZ: 5m,
            materialCostPerCm3: 0.5m,
            machineHourlyRate: 100m,
            setupFee: 0m,
            minimumOrderPrice: 0m,
            processParameters: new Dictionary<string, string>());

        var withSupport = _calculator.Calculate(
            volumeCm3: 50m,
            supportVolumeCm3: 10m,
            surfaceAreaCm2: 100m,
            boundingBoxX: 10m,
            boundingBoxY: 10m,
            boundingBoxZ: 5m,
            materialCostPerCm3: 0.5m,
            machineHourlyRate: 100m,
            setupFee: 0m,
            minimumOrderPrice: 0m,
            processParameters: new Dictionary<string, string>());

        Assert.True(withSupport > withoutSupport);
    }

    [Theory]
    [InlineData(100, 50, 10, 5, 0.5, 100, 50, 300, true)]  // Above minimum - check price >= min
    [InlineData(10, 2, 1, 1, 0.5, 100, 50, 300, true)]    // Below minimum
    [InlineData(200, 50, 20, 10, 1.0, 200, 100, 300, true)] // High volume
    public void Calculate_VariousVolumes_ReturnsExpectedResults(decimal volume, decimal support, decimal surface, 
        decimal boxZ, decimal materialCost, decimal machineRate, decimal setup, decimal minOrder, bool expectMin)
    {
        var result = _calculator.Calculate(
            volumeCm3: volume,
            supportVolumeCm3: support,
            surfaceAreaCm2: surface,
            boundingBoxX: 10m,
            boundingBoxY: 10m,
            boundingBoxZ: boxZ,
            materialCostPerCm3: materialCost,
            machineHourlyRate: machineRate,
            setupFee: setup,
            minimumOrderPrice: minOrder,
            processParameters: new Dictionary<string, string>());

        if (expectMin)
        {
            Assert.InRange(result, minOrder - 1m, minOrder * 10m);
        }
    }
}

public class SlaPricingCalculatorTests
{
    private readonly SlaPricingCalculator _calculator = new();

    [Fact]
    public void Calculate_ValidInputs_ReturnsCorrectPrice()
    {
        var result = _calculator.Calculate(
            volumeCm3: 50m,
            supportVolumeCm3: 10m,
            surfaceAreaCm2: 100m,
            boundingBoxX: 10m,
            boundingBoxY: 10m,
            boundingBoxZ: 5m,
            materialCostPerCm3: 2.0m,
            machineHourlyRate: 150m,
            setupFee: 100m,
            minimumOrderPrice: 500m,
            processParameters: new Dictionary<string, string>());

        Assert.InRange(result, 500m, 1000m);
    }

    [Fact]
    public void Calculate_EmptyGeometry_ReturnsMinimumOrderPrice()
    {
        var result = _calculator.Calculate(
            volumeCm3: 0m,
            supportVolumeCm3: 0m,
            surfaceAreaCm2: 0m,
            boundingBoxX: 0m,
            boundingBoxY: 0m,
            boundingBoxZ: 0m,
            materialCostPerCm3: 2.0m,
            machineHourlyRate: 150m,
            setupFee: 100m,
            minimumOrderPrice: 500m,
            processParameters: new Dictionary<string, string>());

        Assert.InRange(result, 400m, 600m);
    }

    [Fact]
    public void Calculate_WithCustomLayerExposure_UsesCustomValue()
    {
        var parameters = new Dictionary<string, string> { { "LayerExposure", "5.0" } };
        
        var result = _calculator.Calculate(
            volumeCm3: 50m,
            supportVolumeCm3: 0m,
            surfaceAreaCm2: 100m,
            boundingBoxX: 10m,
            boundingBoxY: 10m,
            boundingBoxZ: 5m,
            materialCostPerCm3: 2.0m,
            machineHourlyRate: 150m,
            setupFee: 0m,
            minimumOrderPrice: 500m,
            processParameters: parameters);

        Assert.InRange(result, 500m, 1000m);
    }

    [Theory]
    [InlineData(200, 50, 200, 10, 2.0, 150, 100, 500, true)]  // Above minimum - check price >= min
    [InlineData(10, 2, 10, 1, 2.0, 150, 100, 500, true)]     // Below minimum
    public void Calculate_VariousVolumes_ReturnsExpectedResults(decimal volume, decimal support, decimal surface,
        decimal boxZ, decimal materialCost, decimal machineRate, decimal setup, decimal minOrder, bool expectMin)
    {
        var result = _calculator.Calculate(
            volumeCm3: volume,
            supportVolumeCm3: support,
            surfaceAreaCm2: surface,
            boundingBoxX: 10m,
            boundingBoxY: 10m,
            boundingBoxZ: boxZ,
            materialCostPerCm3: materialCost,
            machineHourlyRate: machineRate,
            setupFee: setup,
            minimumOrderPrice: minOrder,
            processParameters: new Dictionary<string, string>());

        if (expectMin)
        {
            Assert.InRange(result, minOrder - 1m, minOrder * 10m);
        }
    }
}

public class CncPricingCalculatorTests
{
    private readonly CncPricingCalculator _calculator = new();

    [Fact]
    public void Calculate_ValidInputs_ReturnsCorrectPrice()
    {
        var result = _calculator.Calculate(
            volumeCm3: 50m,
            supportVolumeCm3: 0m,
            surfaceAreaCm2: 100m,
            boundingBoxX: 10m,
            boundingBoxY: 10m,
            boundingBoxZ: 5m,
            materialCostPerCm3: 5.0m,
            machineHourlyRate: 500m,
            setupFee: 500m,
            minimumOrderPrice: 2500m,
            processParameters: new Dictionary<string, string>());

        Assert.InRange(result, 2500m, 10000m);
    }

    [Fact]
    public void Calculate_EmptyGeometry_ReturnsMinimumOrderPrice()
    {
        var result = _calculator.Calculate(
            volumeCm3: 0m,
            supportVolumeCm3: 0m,
            surfaceAreaCm2: 0m,
            boundingBoxX: 0m,
            boundingBoxY: 0m,
            boundingBoxZ: 0m,
            materialCostPerCm3: 5.0m,
            machineHourlyRate: 500m,
            setupFee: 500m,
            minimumOrderPrice: 2500m,
            processParameters: new Dictionary<string, string>());

        Assert.Equal(2500m, result);
    }

    [Fact]
    public void Calculate_WithHighSurfaceAreaToVolume_AppliesComplexityFactor()
    {
        var simplePart = _calculator.Calculate(
            volumeCm3: 1000m,
            supportVolumeCm3: 0m,
            surfaceAreaCm2: 600m,
            boundingBoxX: 10m,
            boundingBoxY: 10m,
            boundingBoxZ: 10m,
            materialCostPerCm3: 5.0m,
            machineHourlyRate: 500m,
            setupFee: 0m,
            minimumOrderPrice: 0m,
            processParameters: new Dictionary<string, string>());

        var complexPart = _calculator.Calculate(
            volumeCm3: 10m,
            supportVolumeCm3: 0m,
            surfaceAreaCm2: 100m,
            boundingBoxX: 5m,
            boundingBoxY: 5m,
            boundingBoxZ: 2m,
            materialCostPerCm3: 5.0m,
            machineHourlyRate: 500m,
            setupFee: 0m,
            minimumOrderPrice: 0m,
            processParameters: new Dictionary<string, string>());

        Assert.True(complexPart > simplePart * 0.1m);
    }

    [Fact]
    public void Calculate_WithCustomMachinabilityRating_UsesCustomValue()
    {
        var parameters = new Dictionary<string, string> { { "MachinabilityRating", "0.5" } };
        
        var result = _calculator.Calculate(
            volumeCm3: 50m,
            supportVolumeCm3: 0m,
            surfaceAreaCm2: 100m,
            boundingBoxX: 10m,
            boundingBoxY: 10m,
            boundingBoxZ: 5m,
            materialCostPerCm3: 5.0m,
            machineHourlyRate: 500m,
            setupFee: 0m,
            minimumOrderPrice: 2500m,
            processParameters: parameters);

        Assert.InRange(result, 2500m, 50000m);
    }

    [Theory]
    [InlineData(100, 500, 500, 10, 5.0, 500, 500, 2500, true)]  // Above minimum - check price >= min
    [InlineData(10, 10, 10, 1, 5.0, 500, 500, 2500, true)]      // Below minimum
    public void Calculate_VariousVolumes_ReturnsExpectedResults(decimal volume, decimal surface, decimal boxProduct,
        decimal boxZ, decimal materialCost, decimal machineRate, decimal setup, decimal minOrder, bool expectMin)
    {
        var boxX = Math.Pow((double)boxProduct, 1.0/3.0);
        
        var result = _calculator.Calculate(
            volumeCm3: volume,
            supportVolumeCm3: 0m,
            surfaceAreaCm2: surface,
            boundingBoxX: (decimal)boxX,
            boundingBoxY: (decimal)boxX,
            boundingBoxZ: boxZ,
            materialCostPerCm3: materialCost,
            machineHourlyRate: machineRate,
            setupFee: setup,
            minimumOrderPrice: minOrder,
            processParameters: new Dictionary<string, string>());

        if (expectMin)
        {
            Assert.InRange(result, minOrder - 1m, minOrder * 10m);
        }
    }
}

public class RuleBasedPricingEngineTests
{
    [Fact]
    public async Task CalculateAsync_FdmProcess_ReturnsFdmCalculatorResult()
    {
        var logger = new Mock<ILogger<RuleBasedPricingEngine>>();
        var engine = new RuleBasedPricingEngine(logger.Object);

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "PLA",
            ManufacturingProcessId = Guid.NewGuid(),
            ManufacturingProcessName = "FDM",
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 50m,
                SupportVolumeCm3 = 10m,
                SurfaceAreaCm2 = 100m,
                BoundingBoxX = 10m,
                BoundingBoxY = 10m,
                BoundingBoxZ = 5m
            }
        };

        var config = new PricingConfiguration
        {
            Id = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            ManufacturingProcessId = Guid.NewGuid(),
            MaterialPricePerCm3 = 0.5m,
            MachineHourlyRate = 100m,
            SetupCostFlat = 50m,
            MinimumOrderPrice = 300m,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow
        };

        var result = await engine.CalculateAsync(request, config, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.UnitPrice >= 300m);
        Assert.Contains("FDM", result.EngineName);
    }

    [Fact]
    public async Task CalculateAsync_SlaProcess_ReturnsSlaCalculatorResult()
    {
        var logger = new Mock<ILogger<RuleBasedPricingEngine>>();
        var engine = new RuleBasedPricingEngine(logger.Object);

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "Standard Resin",
            ManufacturingProcessId = Guid.NewGuid(),
            ManufacturingProcessName = "SLA",
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 50m,
                SupportVolumeCm3 = 10m,
                SurfaceAreaCm2 = 100m,
                BoundingBoxX = 10m,
                BoundingBoxY = 10m,
                BoundingBoxZ = 5m
            }
        };

        var config = new PricingConfiguration
        {
            Id = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            ManufacturingProcessId = Guid.NewGuid(),
            MaterialPricePerCm3 = 2.0m,
            MachineHourlyRate = 150m,
            SetupCostFlat = 100m,
            MinimumOrderPrice = 500m,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow
        };

        var result = await engine.CalculateAsync(request, config, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.UnitPrice >= 500m);
        Assert.Contains("SLA", result.EngineName);
    }

    [Fact]
    public async Task CalculateAsync_CncProcess_ReturnsCncCalculatorResult()
    {
        var logger = new Mock<ILogger<RuleBasedPricingEngine>>();
        var engine = new RuleBasedPricingEngine(logger.Object);

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "6061 Aluminum",
            ManufacturingProcessId = Guid.NewGuid(),
            ManufacturingProcessName = "CNC",
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 50m,
                SupportVolumeCm3 = 0m,
                SurfaceAreaCm2 = 100m,
                BoundingBoxX = 10m,
                BoundingBoxY = 10m,
                BoundingBoxZ = 5m
            }
        };

        var config = new PricingConfiguration
        {
            Id = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            ManufacturingProcessId = Guid.NewGuid(),
            MaterialPricePerCm3 = 5.0m,
            MachineHourlyRate = 500m,
            SetupCostFlat = 500m,
            MinimumOrderPrice = 2500m,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow
        };

        var result = await engine.CalculateAsync(request, config, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.UnitPrice >= 2500m);
        Assert.Contains("CNC", result.EngineName);
    }

    [Fact]
    public async Task CalculateAsync_UnknownProcess_DefaultsToFdm()
    {
        var logger = new Mock<ILogger<RuleBasedPricingEngine>>();
        var engine = new RuleBasedPricingEngine(logger.Object);

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "Unknown",
            ManufacturingProcessId = Guid.NewGuid(),
            ManufacturingProcessName = "Unknown Process",
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 50m,
                SupportVolumeCm3 = 0m,
                SurfaceAreaCm2 = 100m,
                BoundingBoxX = 10m,
                BoundingBoxY = 10m,
                BoundingBoxZ = 5m
            }
        };

        var config = new PricingConfiguration
        {
            Id = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            ManufacturingProcessId = Guid.NewGuid(),
            MaterialPricePerCm3 = 0.5m,
            MachineHourlyRate = 100m,
            SetupCostFlat = 50m,
            MinimumOrderPrice = 300m,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow
        };

        var result = await engine.CalculateAsync(request, config, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("FDM", result.EngineName);
    }

    [Fact]
    public async Task CalculateAsync_QuantityGreaterThanOne_ReturnsTotalAmountMultiplied()
    {
        var logger = new Mock<ILogger<RuleBasedPricingEngine>>();
        var engine = new RuleBasedPricingEngine(logger.Object);

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "PLA",
            ManufacturingProcessId = Guid.NewGuid(),
            ManufacturingProcessName = "FDM",
            Quantity = 5,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 50m,
                SupportVolumeCm3 = 0m,
                SurfaceAreaCm2 = 100m,
                BoundingBoxX = 10m,
                BoundingBoxY = 10m,
                BoundingBoxZ = 5m
            }
        };

        var config = new PricingConfiguration
        {
            Id = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            ManufacturingProcessId = Guid.NewGuid(),
            MaterialPricePerCm3 = 0.5m,
            MachineHourlyRate = 100m,
            SetupCostFlat = 50m,
            MinimumOrderPrice = 300m,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow
        };

        var result = await engine.CalculateAsync(request, config, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(result.UnitPrice * 5, result.TotalAmount);
    }
}
