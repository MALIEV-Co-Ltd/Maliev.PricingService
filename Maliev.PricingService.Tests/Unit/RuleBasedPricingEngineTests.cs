using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Data.Entities;
using Xunit;

namespace Maliev.PricingService.Tests.Unit;

public class RuleBasedPricingEngineTests
{
    private readonly RuleBasedPricingEngine _sut;

    public RuleBasedPricingEngineTests()
    {
        _sut = new RuleBasedPricingEngine();
    }

    [Fact]
    public async Task CalculateAsync_BasicCalculation_ReturnsCorrectTotals()
    {
        // Arrange
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "MAT",
            ManufacturingProcessId = Guid.NewGuid(),
            ManufacturingProcessName = "PROC",
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 10,
                SupportVolumeCm3 = 2,
                SurfaceAreaCm2 = 100, // Ratio 10
                BoundingBoxX = 10,
                BoundingBoxY = 10,
                BoundingBoxZ = 10,
                IsManifold = true,
                TriangleCount = 100
            },
            Quantity = 1
        };

        var config = new PricingConfiguration
        {
            MaterialPricePerCm3 = 10,
            SupportMaterialPricePerCm3 = 5,
            PrintSpeedCm3PerHour = 10,
            MachineHourlyRate = 50,
            SetupCostFlat = 20,
            MarginMultiplier = 1.2m,
            ComplexityThreshold = 20, // High threshold, no surcharge
            MinimumOrderPrice = 0
        };

        // Expected:
        // Material: 10 * 10 = 100
        // Support: 2 * 5 = 10
        // Total Material: 110
        // Time: 10 / 10 = 1 hour
        // Machine: 1 * 50 = 50
        // Setup: 20
        // Base: 110 + 50 + 20 = 180
        // Surcharge: 0
        // Subtotal: 180
        // Margin: 180 * (1.2 - 1) = 36
        // Unit: 180 + 36 = 216
        // Total: 216

        // Act
        var result = await _sut.CalculateAsync(request, config);

        // Assert
        Assert.Equal(100, result.MaterialCost);
        Assert.Equal(10, result.SupportMaterialCost);
        Assert.Equal(50, result.MachineTimeCost);
        Assert.Equal(20, result.SetupCost);
        Assert.Equal(0, result.ComplexitySurcharge);
        Assert.Equal(180, result.SubtotalBeforeMargin);
        Assert.Equal(36, result.MarginAmount);
        Assert.Equal(216, result.TotalUnitPrice);
        Assert.Equal(216, result.TotalPrice);
    }

    [Fact]
    public async Task CalculateAsync_WithComplexitySurcharge_AppliesSurcharge()
    {
        // Arrange
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "MAT",
            ManufacturingProcessId = Guid.NewGuid(),
            ManufacturingProcessName = "PROC",
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 10,
                SupportVolumeCm3 = 0,
                SurfaceAreaCm2 = 100, // Ratio 10
                BoundingBoxX = 10,
                BoundingBoxY = 10,
                BoundingBoxZ = 10,
                IsManifold = true,
                TriangleCount = 100
            },
            Quantity = 1
        };

        var config = new PricingConfiguration
        {
            MaterialPricePerCm3 = 10,
            SupportMaterialPricePerCm3 = 5,
            PrintSpeedCm3PerHour = 10,
            MachineHourlyRate = 50,
            SetupCostFlat = 0,
            MarginMultiplier = 1.0m, // No margin for simplicity
            ComplexityThreshold = 5, // Ratio 10 > 5, applies surcharge
            ComplexitySurchargePercent = 10 // 10%
        };

        // Expected:
        // Material: 100
        // Machine: 50
        // Base: 150
        // Surcharge: 150 * 0.10 = 15
        // Subtotal: 165
        // Total: 165

        // Act
        var result = await _sut.CalculateAsync(request, config);

        // Assert
        Assert.Equal(15, result.ComplexitySurcharge);
        Assert.Equal(165, result.TotalUnitPrice);
    }

    [Fact]
    public async Task CalculateAsync_BelowMinimumPrice_ReturnsMinimumPrice()
    {
        // Arrange
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "MAT",
            ManufacturingProcessId = Guid.NewGuid(),
            ManufacturingProcessName = "PROC",
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 1,
                SupportVolumeCm3 = 0,
                SurfaceAreaCm2 = 1,
                BoundingBoxX = 1,
                BoundingBoxY = 1,
                BoundingBoxZ = 1,
                IsManifold = true,
                TriangleCount = 100
            },
            Quantity = 1
        };

        var config = new PricingConfiguration
        {
            MaterialPricePerCm3 = 1,
            SupportMaterialPricePerCm3 = 1,
            PrintSpeedCm3PerHour = 100,
            MachineHourlyRate = 1,
            SetupCostFlat = 0,
            MarginMultiplier = 1.0m,
            MinimumOrderPrice = 100
        };

        // Cost is very low, < 100

        // Act
        var result = await _sut.CalculateAsync(request, config);

        // Assert
        Assert.Equal(100, result.TotalUnitPrice);
        Assert.Equal(100, result.TotalPrice);
    }
}
