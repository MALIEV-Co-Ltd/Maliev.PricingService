namespace Maliev.PricingService.Tests.Unit.Calculators;

using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Api.Services.Calculators;
using System.Threading.Tasks;
using Xunit;

public class SlaPricingCalculatorTests
{
    private readonly SlaPricingCalculator _calculator = new();

    [Fact]
    public async Task CalculateAsync_StandardResin_ReturnsCorrectPrice()
    {
        // Arrange
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "RESIN",
            Technology = ManufacturingTechnology.Sla,
            LayerHeightMm = 0.05m,
            SupportEnabled = false,
            HeightMm = 50.0m,
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 10.0m,
                SupportVolumeCm3 = 0,
                SurfaceAreaCm2 = 50.0m,
                BoundingBoxX = 30.0m,
                BoundingBoxY = 30.0m,
                BoundingBoxZ = 50.0m,
                IsManifold = true,
                TriangleCount = 1000
            }
        };

        var material = new MaterialData
        {
            Density = 1.1m,
            CostPerKg = 2000.0m,
            SlaLayerExposure = 2.5m,
            SlaLiftTime = 1.5m
        };

        var rates = new MachineRates
        {
            SlaMachineHourlyRate = 200.0m,
            SlaSetupFee = 80.0m
        };

        // Act
        var result = await _calculator.CalculateAsync(request, material, rates);

        // Assert
        // totalLayers = 50 / 0.05 = 1000
        // printTimeHours = 1000 * (2.5 + 1.5) / 3600 = 1.111h
        // machineTimeCost = 1.111 * 200 = 222.22
        // materialCost = 10 * 1.1 * 2 = 22.0
        // subtotal = 22 + 222.22 + 80 = 324.22
        // total = Max(324.22, 500) = 500
        Assert.Equal(500m, result.TotalUnitPrice);
    }
}
