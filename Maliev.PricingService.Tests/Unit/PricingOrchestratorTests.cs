using Maliev.PricingService.Api.Interfaces;
using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Data;
using Maliev.PricingService.Data.Entities;
using Maliev.PricingService.Tests.TestFixtures;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ServicePricingStrategy = Maliev.PricingService.Api.Services.PricingStrategy;

namespace Maliev.PricingService.Tests.Unit;

public class PricingOrchestratorTests : IClassFixture<PricingServiceTestFactory>
{
    private readonly PricingServiceTestFactory _factory;
    private readonly Mock<IPricingEngine> _mockEngine;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<ILogger<PricingOrchestrator>> _mockLogger;

    public PricingOrchestratorTests(PricingServiceTestFactory factory)
    {
        _factory = factory;
        _mockEngine = new Mock<IPricingEngine>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockLogger = new Mock<ILogger<PricingOrchestrator>>();
    }

    [Fact]
    public async Task CalculatePriceAsync_WithValidConfig_ReturnsResultAndSavesAudit()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        var configId = Guid.Empty;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();

        // Ensure clean state for this test run
        var existingConfigs = await db.PricingConfigurations.ToListAsync();
        db.PricingConfigurations.RemoveRange(existingConfigs);
        await db.SaveChangesAsync();

        var config = new PricingConfiguration
        {
            MaterialId = materialId,
            ManufacturingProcessId = processId,
            MaterialPricePerCm3 = 10,
            SupportMaterialPricePerCm3 = 5,
            PrintSpeedCm3PerHour = 100,
            MachineHourlyRate = 50,
            SetupCostFlat = 25,
            MarginMultiplier = 1.2m,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            CreatedBy = "Test"
        };
        db.PricingConfigurations.Add(config);
        await db.SaveChangesAsync();
        configId = config.Id; // Capture generated ID

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = materialId,
            MaterialCode = "MAT",
            ManufacturingProcessId = processId,
            ManufacturingProcessName = "PROC",
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

        _mockEngine.Setup(e => e.CalculateAsync(It.IsAny<PricingRequest>(), It.IsAny<PricingConfiguration>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(expectedResult);

        var orchestrator = new PricingOrchestrator(db, _mockEngine.Object, _memoryCache, _mockPublishEndpoint.Object, _mockLogger.Object);

        // Act
        var result = await orchestrator.CalculatePriceAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(95.00m, result.TotalUnitPrice); // 5% discount applied (100 * 0.95)
        
        // Verify Audit
        var audit = await db.PricingAuditRecords.FirstOrDefaultAsync(a => a.FileId == request.FileId);
        Assert.NotNull(audit);
        Assert.Equal(configId, audit.PricingConfigurationId);
    }

    [Fact]
    public async Task CalculatePriceAsync_WithRealEngine_ReturnsSuccess()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();

        // Ensure clean state
        var existingConfigs = await db.PricingConfigurations.ToListAsync();
        db.PricingConfigurations.RemoveRange(existingConfigs);
        await db.SaveChangesAsync();

        var config = new PricingConfiguration
        {
            MaterialId = materialId,
            ManufacturingProcessId = processId,
            MaterialPricePerCm3 = 10,
            SupportMaterialPricePerCm3 = 5,
            PrintSpeedCm3PerHour = 100,
            MachineHourlyRate = 50,
            SetupCostFlat = 25,
            MarginMultiplier = 1.2m,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            CreatedBy = "Test"
        };
        db.PricingConfigurations.Add(config);
        await db.SaveChangesAsync();

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = materialId,
            MaterialCode = "MAT",
            ManufacturingProcessId = processId,
            ManufacturingProcessName = "PROC",
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

        var realEngine = new RuleBasedPricingEngine();
        var orchestrator = new PricingOrchestrator(db, realEngine, _memoryCache, _mockPublishEndpoint.Object, _mockLogger.Object);

        // Act
        var result = await orchestrator.CalculatePriceAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TotalPrice > 0);
    }

    [Fact]
    public async Task CalculatePriceAsync_WithMissingConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(), // Random ID
            MaterialCode = "MAT",
            ManufacturingProcessId = Guid.NewGuid(), // Random ID
            ManufacturingProcessName = "PROC",
            Geometry = new GeometryMetrics 
            { 
                VolumeCm3 = 10, 
                SupportVolumeCm3 = 0, 
                SurfaceAreaCm2 = 10,
                BoundingBoxX = 10,
                BoundingBoxY = 10,
                BoundingBoxZ = 10,
                IsManifold = true,
                TriangleCount = 100 
            },
            Quantity = 1
        };

        var orchestrator = new PricingOrchestrator(db, _mockEngine.Object, _memoryCache, _mockPublishEndpoint.Object, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.CalculatePriceAsync(request));
    }

    [Fact]
    public async Task CalculatePriceAsync_WhenEngineFails_UsesFallbackIfAvailable()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        var cacheKey = $"PriceFallback_{materialId}_{processId}";
        
        var fallbackResult = new PricingResult 
        { 
            TotalUnitPrice = 999, 
            TotalPrice = 999,
            Strategy = ServicePricingStrategy.RuleBased,
            MaterialCost = 0,
            SupportMaterialCost = 0,
            MachineTimeCost = 0,
            SetupCost = 0,
            ComplexitySurcharge = 0,
            SubtotalBeforeMargin = 0,
            MarginAmount = 0,
            ConfidenceLevel = 0.5m,
            ValidUntil = DateTime.UtcNow.AddDays(7),
            CalculationDuration = TimeSpan.Zero
        };
        _memoryCache.Set(cacheKey, fallbackResult);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();

        // We need a config to exist, otherwise it fails before engine
        var config = new PricingConfiguration
        {
            MaterialId = materialId,
            ManufacturingProcessId = processId,
            MaterialPricePerCm3 = 10,
            SupportMaterialPricePerCm3 = 5,
            PrintSpeedCm3PerHour = 100,
            MachineHourlyRate = 50,
            SetupCostFlat = 25,
            MarginMultiplier = 1.2m,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            CreatedBy = "Test"
        };
        db.PricingConfigurations.Add(config);
        await db.SaveChangesAsync();

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = materialId,
            MaterialCode = "MAT",
            ManufacturingProcessId = processId,
            ManufacturingProcessName = "PROC",
            Geometry = new GeometryMetrics 
            { 
                VolumeCm3 = 10, 
                SupportVolumeCm3 = 0, 
                SurfaceAreaCm2 = 10,
                BoundingBoxX = 10,
                BoundingBoxY = 10,
                BoundingBoxZ = 10,
                IsManifold = true,
                TriangleCount = 100
            },
            Quantity = 1
        };

        _mockEngine.Setup(e => e.CalculateAsync(It.IsAny<PricingRequest>(), It.IsAny<PricingConfiguration>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new Exception("Engine failed"));

        var orchestrator = new PricingOrchestrator(db, _mockEngine.Object, _memoryCache, _mockPublishEndpoint.Object, _mockLogger.Object);

        // Act
        var result = await orchestrator.CalculatePriceAsync(request);

        // Assert
        Assert.Equal(999, result.TotalUnitPrice);
    }
}
