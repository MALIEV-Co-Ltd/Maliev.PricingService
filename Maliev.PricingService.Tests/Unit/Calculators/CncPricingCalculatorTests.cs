namespace Maliev.PricingService.Tests.Unit.Calculators;

using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Api.Services.Calculators;
using System.Threading.Tasks;
using Xunit;

public class CncPricingCalculatorTests
{
    private readonly CncPricingCalculator _calculator = new();

    [Fact]
    public async Task CalculateAsync_AluminumBlock_ReturnsCorrectPrice()
    {
        // Arrange
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "AL6061",
            Technology = ManufacturingTechnology.Cnc,
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 100.0m, // Part is 100 cm3
                SupportVolumeCm3 = 0,
                SurfaceAreaCm2 = 150.0m,
                BoundingBoxX = 100.0m,
                BoundingBoxY = 100.0m,
                BoundingBoxZ = 100.0m, // Block is 100x100x100 = 1,000,000 mm3 = 1000 cm3
                IsManifold = true,
                TriangleCount = 1000
            }
        };

        var material = new MaterialData
        {
            Density = 2.7m,
            CostPerKg = 400.0m,
            CncMachinabilityRating = 0.8m
        };

        var rates = new MachineRates
        {
            CncMachineHourlyRate = 800.0m,
            CncSetupFee = 500.0m,
            CncMaterialRemovalRate = 50000.0m // mm3/hour
        };

        // Act
        var result = await _calculator.CalculateAsync(request, material, rates);

        // Assert
        // blockVolumeMm3 = 100*100*100 = 1,000,000
        // partVolumeMm3 = 100,000
        // removalVolumeMm3 = 900,000
        // effectiveMrr = 50000 * 0.8 = 40000
        // machiningTimeHours = 900,000 / 40000 = 22.5h
        // blockWeightGrams = 1,000,000 * 2.7 / 1000 = 2700g
        // blockCost = 2700 * 0.4 = 1080
        // complexityFactor = 150 / 100 = 1.5
        // machineTimeCost = 22.5 * 800 * 1.5 = 27000
        // total = 1080 + 27000 + 500 = 28580
        Assert.Equal(28580m, result.TotalUnitPrice);
    }
}
