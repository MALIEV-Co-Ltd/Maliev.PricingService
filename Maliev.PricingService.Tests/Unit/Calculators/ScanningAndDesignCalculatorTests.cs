namespace Maliev.PricingService.Tests.Unit.Calculators;

using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Api.Services.Calculators;
using System.Threading.Tasks;
using Xunit;

public class ScanningAndDesignCalculatorTests
{
    [Fact]
    public async Task ScanningCalculateAsync_ReturnsCorrectTierPrice()
    {
        var calculator = new ScanningPricingCalculator();
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "SCAN",
            Technology = ManufacturingTechnology.Scanning,
            ScanningTier = "RawScan",
            Quantity = 1,
            Geometry = new GeometryMetrics { VolumeCm3 = 1, SupportVolumeCm3 = 0, SurfaceAreaCm2 = 1, BoundingBoxX = 1, BoundingBoxY = 1, BoundingBoxZ = 1, IsManifold = true, TriangleCount = 1 }
        };

        var result = await calculator.CalculateAsync(request, new MaterialData(), new MachineRates());
        Assert.Equal(2500m, result.TotalUnitPrice);

        var request2 = request with { ScanningTier = "ReverseEngineering" };
        var result2 = await calculator.CalculateAsync(request2, new MaterialData(), new MachineRates());
        Assert.Equal(4500m, result2.TotalUnitPrice);
    }

    [Fact]
    public async Task DesignCalculateAsync_ReturnsFixedPrice()
    {
        var calculator = new DesignPricingCalculator();
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "DESIGN",
            Technology = ManufacturingTechnology.Design,
            Quantity = 1,
            Geometry = new GeometryMetrics { VolumeCm3 = 1, SupportVolumeCm3 = 0, SurfaceAreaCm2 = 1, BoundingBoxX = 1, BoundingBoxY = 1, BoundingBoxZ = 1, IsManifold = true, TriangleCount = 1 }
        };

        var result = await calculator.CalculateAsync(request, new MaterialData(), new MachineRates());
        Assert.Equal(500m, result.TotalUnitPrice);
    }
}
