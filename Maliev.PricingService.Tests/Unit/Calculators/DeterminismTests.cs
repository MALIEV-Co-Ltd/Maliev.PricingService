namespace Maliev.PricingService.Tests.Unit.Calculators;

using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Api.Services.Calculators;
using System.Threading.Tasks;
using Xunit;

public class DeterminismTests
{
    [Fact]
    public async Task CalculateAsync_IdenticalInputs_ProduceIdenticalOutputs()
    {
        // Arrange
        var calculator = new FdmPricingCalculator();
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
        var result1 = await calculator.CalculateAsync(request, material, rates);
        var result2 = await calculator.CalculateAsync(request, material, rates);

        // Assert
        Assert.Equal(result1.TotalUnitPrice, result2.TotalUnitPrice);
        Assert.Equal(result1.MaterialCost, result2.MaterialCost);
        Assert.Equal(result1.MachineTimeCost, result2.MachineTimeCost);
        Assert.Equal(result1.SetupCost, result2.SetupCost);
    }
}
