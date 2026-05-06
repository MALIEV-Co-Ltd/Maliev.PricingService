using Maliev.MessagingContracts.Contracts.Pricing;
using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Application.Services;
using Maliev.PricingService.Domain.Entities;
using Maliev.PricingService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.PostgreSql;

namespace Maliev.PricingService.Tests.Unit;

public sealed class PricingOrchestratorPublishTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .WithDatabase("pricing_publish_tests")
        .Build();
    private PricingDbContext? _db;

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
        _db = new PricingDbContext(
            new DbContextOptionsBuilder<PricingDbContext>()
                .UseNpgsql(_postgresContainer.GetConnectionString())
                .Options);

        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_db is not null)
            await _db.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task CalculatePriceAsync_WhenPriceCalculatedPublishIsCanceled_ReturnsResultWithoutWarning()
    {
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        SeedPricingConfiguration(materialId, processId);
        var db = _db ?? throw new InvalidOperationException("Test database was not initialized.");

        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<PriceCalculatedEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Publisher confirm timed out."));

        var logger = new Mock<ILogger<PricingOrchestrator>>();
        var orchestrator = new PricingOrchestrator(
            db,
            CreatePricingEngine(),
            CreateJobServiceClient(),
            logger.Object,
            publishEndpoint.Object,
            new VolumeDiscountResolver(db),
            CreateCurrencyServiceClient());

        var result = await orchestrator.CalculatePriceAsync(CreateRequest(materialId, processId));

        Assert.NotEqual(Guid.Empty, result.AuditId);
        VerifyNoLogLevel(logger, LogLevel.Warning);
        VerifyLogLevel(logger, LogLevel.Debug);
    }

    private void SeedPricingConfiguration(Guid materialId, Guid processId)
    {
        var db = _db ?? throw new InvalidOperationException("Test database was not initialized.");
        db.Configurations.Add(new PricingConfiguration
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            ManufacturingProcessId = processId,
            MaterialPricePerCm3 = 1m,
            SupportMaterialPricePerCm3 = 0m,
            MachineHourlyRate = 100m,
            PrintSpeedCm3PerHour = 10m,
            SetupCostFlat = 25m,
            MinimumOrderPrice = 10m,
            MarginMultiplier = 1.5m,
            ComplexityThreshold = 6m,
            ComplexitySurchargePercent = 0m,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        });
        db.MachineCapacityConfigs.Add(new MachineCapacityConfig
        {
            Id = Guid.NewGuid(),
            ProcessType = "FDM",
            MachineCount = 1,
            AvgThroughputPartsPerDay = 10m,
            SetupTimeDays = 1,
            ShippingBufferDays = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    private static IPricingEngine CreatePricingEngine()
    {
        var engine = new Mock<IPricingEngine>();
        engine
            .Setup(e => e.CalculateAsync(
                It.IsAny<PricingRequest>(),
                It.IsAny<PricingConfiguration>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EngineResult(
                new CostBreakdown(
                    MaterialCost: 10m,
                    SupportMaterialCost: 0m,
                    MachineTimeCost: 5m,
                    SetupCost: 25m,
                    DfmSurcharge: 0m,
                    ComplexitySurcharge: 0m,
                    SubtotalBeforeMargin: 40m,
                    MinimumOrderPriceFloor: 10m),
                "TestEngine"));
        return engine.Object;
    }

    private static IJobServiceClient CreateJobServiceClient()
    {
        var client = new Mock<IJobServiceClient>();
        client
            .Setup(c => c.GetQueueDepthByTechnologyAsync(
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return client.Object;
    }

    private static ICurrencyServiceClient CreateCurrencyServiceClient()
    {
        var client = new Mock<ICurrencyServiceClient>();
        client
            .Setup(c => c.GetExchangeRateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1m);
        return client.Object;
    }

    private static PricingRequest CreateRequest(Guid materialId, Guid processId) => new()
    {
        FileId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        MaterialId = materialId,
        MaterialCode = "PLA",
        ManufacturingProcessId = processId,
        ManufacturingProcessName = "FDM",
        Quantity = 1,
        Currency = "THB",
        Geometry = new GeometryMetrics
        {
            VolumeCm3 = 10m,
            SupportVolumeCm3 = 0m,
            SurfaceAreaCm2 = 25m,
            BoundingBoxX = 10m,
            BoundingBoxY = 10m,
            BoundingBoxZ = 10m,
            IsManifold = true,
            TriangleCount = 100,
        },
        CorrelationId = Guid.NewGuid(),
        StoragePath = "projects/test-part.stl",
    };

    private static void VerifyNoLogLevel(
        Mock<ILogger<PricingOrchestrator>> logger,
        LogLevel level)
    {
        logger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((_, _) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    private static void VerifyLogLevel(
        Mock<ILogger<PricingOrchestrator>> logger,
        LogLevel level)
    {
        logger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((_, _) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
