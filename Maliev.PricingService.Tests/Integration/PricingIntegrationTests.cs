namespace Maliev.PricingService.Tests.Integration;

using Maliev.PricingService.Api.Clients;
using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Api.Services.Calculators;
using Maliev.PricingService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using MassTransit;

public class PricingIntegrationTests
{
    [Fact]
    public async Task CalculatePriceAsync_EndToEnd_ReturnsResult()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PricingDbContext>()
            .UseInMemoryDatabase(databaseName: "PricingTestDb")
            .Options;
        var dbContext = new PricingDbContext(options);

        var materialClientMock = new Mock<IMaterialServiceClient>();
        var materialId = Guid.NewGuid();
        materialClientMock.Setup(x => x.GetMaterialAsync(materialId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MaterialDto
            {
                Id = materialId,
                DensityGramPerCm3 = 1.24m,
                CostPerKg = 600,
                ProcessParameters = new Dictionary<string, string>
                {
                    { "FdmVolumetricFlowRate", "15" },
                    { "FdmMinLayerTime", "5" }
                }
            });

        var calculators = new List<IPricingCalculator> { new FdmPricingCalculator() };
        var engine = new RuleBasedPricingEngine(calculators);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<PricingOrchestrator>>();

        var rates = new MachineRates
        {
            FdmMachineHourlyRate = 150,
            FdmSetupFee = 50
        };
        var ratesOptions = Options.Create(rates);

        var orchestrator = new PricingOrchestrator(
            dbContext,
            engine,
            materialClientMock.Object,
            cache,
            publishEndpointMock.Object,
            loggerMock.Object,
            ratesOptions);

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.Empty, // No loyalty discount
            MaterialId = materialId,
            MaterialCode = "PLA",
            Technology = ManufacturingTechnology.Fdm,
            LayerHeightMm = 0.2m,
            SupportEnabled = false,
            HeightMm = 10,
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 1.0m,
                SupportVolumeCm3 = 0,
                SurfaceAreaCm2 = 6.0m,
                BoundingBoxX = 10,
                BoundingBoxY = 10,
                BoundingBoxZ = 10,
                IsManifold = true,
                TriangleCount = 12
            }
        };

        // Act
        var result = await orchestrator.CalculatePriceAsync(request);

        // Assert
        Assert.Equal(300m, result.TotalUnitPrice);
        materialClientMock.Verify(x => x.GetMaterialAsync(materialId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
