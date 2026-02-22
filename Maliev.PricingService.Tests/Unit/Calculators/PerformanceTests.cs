namespace Maliev.PricingService.Tests.Unit.Calculators;

using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Api.Services.Calculators;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

public class PerformanceTests
{
    [Fact]
    public async Task CalculateAsync_CompletesUnderOneSecond()
    {
        // Arrange
        var calculator = new CncPricingCalculator(); // Use CNC as it has most complex formula
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "AL",
            Technology = ManufacturingTechnology.Cnc,
            Quantity = 1,
            Geometry = new GeometryMetrics { VolumeCm3 = 100, SupportVolumeCm3 = 0, SurfaceAreaCm2 = 150, BoundingBoxX = 100, BoundingBoxY = 100, BoundingBoxZ = 100, IsManifold = true, TriangleCount = 1000 }
        };

        var material = new MaterialData { Density = 2.7m, CostPerKg = 400, CncMachinabilityRating = 0.8m };
        var rates = new MachineRates { CncMachineHourlyRate = 800, CncSetupFee = 500, CncMaterialRemovalRate = 50000 };

        // Act
        var sw = Stopwatch.StartNew();
        await calculator.CalculateAsync(request, material, rates);
        sw.Stop();

        // Assert
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Calculation took {sw.ElapsedMilliseconds}ms, which is >= 1000ms");
    }
}
