namespace Maliev.PricingService.Tests.Unit.Calculators;

using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Api.Services.Calculators;
using System.Threading.Tasks;
using Xunit;

public class FdmPricingCalculatorTests
{
    private readonly FdmPricingCalculator _calculator = new();

    [Fact]
    public async Task CalculateAsync_SimpleCube_ReturnsCorrectPrice()
    {
        // Arrange
        // 10x10x10mm cube = 1 cm3
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "PLA",
            Technology = ManufacturingTechnology.Fdm,
            LayerHeightMm = 0.2m,
            SupportEnabled = false,
            HeightMm = 10.0m,
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 1.0m,
                SupportVolumeCm3 = 0,
                SurfaceAreaCm2 = 6.0m,
                BoundingBoxX = 10.0m,
                BoundingBoxY = 10.0m,
                BoundingBoxZ = 10.0m,
                IsManifold = true,
                TriangleCount = 12
            }
        };

        var material = new MaterialData
        {
            Density = 1.24m, // g/cm3
            CostPerKg = 600.0m,
            FdmVolumetricFlowRate = 15.0m, // mm3/s
            FdmMinLayerTime = 5.0m // seconds
        };

        var rates = new MachineRates
        {
            FdmMachineHourlyRate = 150.0m,
            FdmSetupFee = 50.0m
        };

        // Act
        var result = await _calculator.CalculateAsync(request, material, rates);

        // Assert
        // volumeMm3 = 1000
        // weightGrams = 1000 * 1.24 / 1000 = 1.24g
        // materialCost = 1.24 * (600 / 1000) = 0.744
        // totalLayers = 10 / 0.2 = 50
        // calcLayerTimeSec = 1000 / 15 / 50 = 1.33s
        // effectiveLayerTimeSec = Max(1.33, 5) = 5s
        // printTimeHours = (5 * 50) / 3600 = 0.0694h
        // machineTimeCost = 0.0694 * 150 = 10.41
        // subtotal = 0.744 + 10.41 + 50 = 61.154
        // total = Max(61.154, 300) = 300
        Assert.Equal(300m, result.TotalUnitPrice);
    }

    [Fact]
    public async Task CalculateAsync_HollowBasket_AppliesMinLayerTime()
    {
        // Arrange
        // High height, low volume
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "PLA",
            Technology = ManufacturingTechnology.Fdm,
            LayerHeightMm = 0.2m,
            SupportEnabled = false,
            HeightMm = 150.0m,
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 5.0m,
                SupportVolumeCm3 = 0,
                SurfaceAreaCm2 = 100.0m,
                BoundingBoxX = 50.0m,
                BoundingBoxY = 50.0m,
                BoundingBoxZ = 150.0m,
                IsManifold = true,
                TriangleCount = 1000
            }
        };

        var material = new MaterialData
        {
            Density = 1.24m,
            CostPerKg = 600.0m,
            FdmVolumetricFlowRate = 15.0m,
            FdmMinLayerTime = 5.0m
        };

        var rates = new MachineRates
        {
            FdmMachineHourlyRate = 150.0m,
            FdmSetupFee = 50.0m
        };

        // Act
        var result = await _calculator.CalculateAsync(request, material, rates);

        // Assert
        // totalLayers = 150 / 0.2 = 750
        // volumeMm3 = 5000
        // calcLayerTimeSec = 5000 / 15 / 750 = 0.44s
        // effectiveLayerTimeSec = Max(0.44, 5) = 5s
        // printTimeHours = (5 * 750) / 3600 = 1.041666...h
        // materialCost = 5 * 1.24 * 0.6 = 3.72
        // machineTimeCost = 1.041666... * 500 = 520.8333...
        // subtotal = 3.72 + 520.8333... + 50 = 574.5533...
        // total = Max(574.55, 300) = 574.55
        var expensiveRates = rates with { FdmMachineHourlyRate = 500m };
        var result2 = await _calculator.CalculateAsync(request, material, expensiveRates);
        
        Assert.Equal(574.55m, result2.TotalUnitPrice);
    }
}
