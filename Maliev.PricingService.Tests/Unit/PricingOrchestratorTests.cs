using Maliev.PricingService.Api.Interfaces;
using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Api.Clients;
using Maliev.PricingService.Data;
using Maliev.PricingService.Data.Entities;
using Maliev.PricingService.Tests.TestFixtures;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ServicePricingStrategy = Maliev.PricingService.Api.Services.PricingStrategy;

namespace Maliev.PricingService.Tests.Unit;

public class PricingOrchestratorTests : IClassFixture<PricingServiceTestFactory>
{
    private readonly PricingServiceTestFactory _factory;
    private readonly Mock<IPricingEngine> _mockEngine;
    private readonly Mock<IMaterialServiceClient> _mockMaterialClient;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<ILogger<PricingOrchestrator>> _mockLogger;
    private readonly IOptions<MachineRates> _ratesOptions;

    public PricingOrchestratorTests(PricingServiceTestFactory factory)
    {
        _factory = factory;
        _mockEngine = new Mock<IPricingEngine>();
        _mockMaterialClient = new Mock<IMaterialServiceClient>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockLogger = new Mock<ILogger<PricingOrchestrator>>();
        _ratesOptions = Options.Create(new MachineRates());
    }

    [Fact]
    public async Task CalculatePriceAsync_WithValidMaterial_ReturnsResultAndSavesAudit()
    {
        // Arrange
        var materialId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();

        var materialDto = new MaterialDto
        {
            Id = materialId,
            DensityGramPerCm3 = 1.24m,
            CostPerKg = 600,
            ProcessParameters = new Dictionary<string, string>()
        };

        _mockMaterialClient.Setup(m => m.GetMaterialAsync(materialId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(materialDto);

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = materialId,
            MaterialCode = "MAT",
            Technology = ManufacturingTechnology.Fdm,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 10,
                SupportVolumeCm3 = 2,
                SurfaceAreaCm2 = 50,
                BoundingBoxX = 10,
                BoundingBoxY = 10,
                BoundingBoxZ = 10,
                IsManifold = true,
                TriangleCount = 100
            },
            Quantity = 1
        };

        var expectedResult = new PricingResult
        {
            TotalUnitPrice = 100,
            TotalPrice = 100,
            Strategy = ServicePricingStrategy.RuleBased,
            MaterialCost = 10,
            SupportMaterialCost = 10,
            MachineTimeCost = 10,
            SetupCost = 10,
            ComplexitySurcharge = 0,
            SubtotalBeforeMargin = 50,
            MarginAmount = 50,
            ConfidenceLevel = 1.0m,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            CalculationDuration = TimeSpan.FromMilliseconds(100)
        };

        _mockEngine.Setup(e => e.CalculateAsync(It.IsAny<PricingRequest>(), It.IsAny<MaterialData>(), It.IsAny<MachineRates>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(expectedResult);

        var orchestrator = new PricingOrchestrator(db, _mockEngine.Object, _mockMaterialClient.Object, _memoryCache, _mockPublishEndpoint.Object, _mockLogger.Object, _ratesOptions);

        // Act
        var result = await orchestrator.CalculatePriceAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(95.00m, result.TotalUnitPrice); // 5% discount applied

        // Verify Audit
        var audit = await db.PricingAuditRecords.FirstOrDefaultAsync(a => a.FileId == request.FileId);
        Assert.NotNull(audit);
        Assert.Equal("Fdm", audit.Technology);
    }

    [Fact]
    public async Task CalculatePriceAsync_WhenMaterialNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();

        _mockMaterialClient.Setup(m => m.GetMaterialAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MaterialDto)null);

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "MAT",
            Technology = ManufacturingTechnology.Fdm,
            Geometry = new GeometryMetrics { VolumeCm3 = 10, SupportVolumeCm3 = 0, SurfaceAreaCm2 = 10, BoundingBoxX = 10, BoundingBoxY = 10, BoundingBoxZ = 10, IsManifold = true, TriangleCount = 100 },
            Quantity = 1
        };

        var orchestrator = new PricingOrchestrator(db, _mockEngine.Object, _mockMaterialClient.Object, _memoryCache, _mockPublishEndpoint.Object, _mockLogger.Object, _ratesOptions);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.CalculatePriceAsync(request));
    }
}
