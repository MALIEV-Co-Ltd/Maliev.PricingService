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
    public async Task CalculatePriceAsync_PublishesCompatibleV1AndReconstructableV2Events()
    {
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        SeedPricingConfiguration(materialId, processId);
        var db = _db ?? throw new InvalidOperationException("Test database was not initialized.");

        PriceCalculatedEvent? publishedV1 = null;
        PriceCalculatedEventV2? publishedV2 = null;
        var publishEndpoint = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var publishSequence = new MockSequence();
        publishEndpoint
            .InSequence(publishSequence)
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<PriceCalculatedEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback<PriceCalculatedEvent, CancellationToken>((message, _) => publishedV1 = message)
            .Returns(Task.CompletedTask);
        publishEndpoint
            .InSequence(publishSequence)
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<PriceCalculatedEventV2>(),
                It.IsAny<CancellationToken>()))
            .Callback<PriceCalculatedEventV2, CancellationToken>((message, _) => publishedV2 = message)
            .Returns(Task.CompletedTask);

        var orchestrator = new PricingOrchestrator(
            db,
            CreatePricingEngine(),
            CreateJobServiceClient(),
            Mock.Of<ILogger<PricingOrchestrator>>(),
            publishEndpoint.Object,
            new VolumeDiscountResolver(db),
            CreateCurrencyServiceClient());

        var request = CreateRequest(materialId, processId);
        var result = await orchestrator.CalculatePriceAsync(request);
        var auditRecord = await db.AuditRecords.SingleAsync(candidate => candidate.Id == result.AuditId);

        Assert.NotNull(publishedV1);
        Assert.Equal("PriceCalculatedEvent", publishedV1.MessageName);
        Assert.Equal("1.0.0", publishedV1.MessageVersion);
        Assert.Equal(["IntranetBff", "QuotationService"], publishedV1.ConsumedBy);
        Assert.Equal(25d, publishedV1.Payload.Breakdown.SetupCost);
        Assert.Equal((double)auditRecord.ComplexitySurcharge, publishedV1.Payload.Breakdown.ComplexitySurcharge);

        Assert.NotNull(publishedV2);
        Assert.Equal("PriceCalculatedEventV2", publishedV2.MessageName);
        Assert.Equal("2.0.0", publishedV2.MessageVersion);
        Assert.Equal("PricingService", publishedV2.PublishedBy);
        Assert.Empty(publishedV2.ConsumedBy);
        Assert.Equal(request.CorrelationId, publishedV2.CorrelationId);
        Assert.Equal(publishedV1.CorrelationId, publishedV2.CorrelationId);
        Assert.Equal(publishedV1.OccurredAtUtc, publishedV2.OccurredAtUtc);
        Assert.Equal(publishedV1.Payload.CalculatedAt, publishedV2.Payload.CalculatedAt);
        Assert.Equal(25d, publishedV2.Payload.Breakdown.SetupCost);
        Assert.Equal(1.25d, publishedV2.Payload.Breakdown.FixedDfmSurcharge);

        var reconstructedSubtotal =
            publishedV2.Payload.Breakdown.MaterialCost +
            publishedV2.Payload.Breakdown.SupportCost +
            publishedV2.Payload.Breakdown.MachineTimeCost +
            publishedV2.Payload.Breakdown.SetupCost +
            publishedV2.Payload.Breakdown.FixedDfmSurcharge +
            publishedV2.Payload.Breakdown.ComplexitySurcharge;
        Assert.Equal(publishedV2.Payload.Breakdown.SubtotalBeforeMargin, reconstructedSubtotal, precision: 8);
        Assert.Equal(
            publishedV2.Payload.Breakdown.TotalPrice,
            publishedV2.Payload.Breakdown.SubtotalBeforeMargin + publishedV2.Payload.Breakdown.MarginAmount,
            precision: 8);
        Assert.Equal(result.TotalAmount, (decimal)publishedV2.Payload.TotalPrice);

        publishEndpoint.Verify(
            endpoint => endpoint.Publish(It.IsAny<PriceCalculatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        publishEndpoint.Verify(
            endpoint => endpoint.Publish(It.IsAny<PriceCalculatedEventV2>(), It.IsAny<CancellationToken>()),
            Times.Once);
        publishEndpoint.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CalculatePriceAsync_WhenV2PublishFails_PreservesV1DeliveryAndReturnsResult()
    {
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        SeedPricingConfiguration(materialId, processId);
        var db = _db ?? throw new InvalidOperationException("Test database was not initialized.");

        PriceCalculatedEvent? publishedV1 = null;
        var publishEndpoint = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var publishSequence = new MockSequence();
        publishEndpoint
            .InSequence(publishSequence)
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<PriceCalculatedEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback<PriceCalculatedEvent, CancellationToken>((message, _) => publishedV1 = message)
            .Returns(Task.CompletedTask);
        publishEndpoint
            .InSequence(publishSequence)
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<PriceCalculatedEventV2>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("v2 broker route unavailable"));

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
        Assert.NotNull(publishedV1);
        Assert.Equal("PriceCalculatedEvent", publishedV1.MessageName);
        Assert.Equal("1.0.0", publishedV1.MessageVersion);
        VerifyLogLevel(logger, LogLevel.Warning);
        publishEndpoint.Verify(
            endpoint => endpoint.Publish(It.IsAny<PriceCalculatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        publishEndpoint.Verify(
            endpoint => endpoint.Publish(It.IsAny<PriceCalculatedEventV2>(), It.IsAny<CancellationToken>()),
            Times.Once);
        publishEndpoint.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CalculatePriceAsync_WhenPriceCalculatedPublishIsCanceled_ReturnsResultWithoutWarning()
    {
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        SeedPricingConfiguration(materialId, processId);
        var db = _db ?? throw new InvalidOperationException("Test database was not initialized.");

        PriceCalculatedEvent? publishedEvent = null;
        PriceCalculatedEventV2? publishedV2 = null;
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<PriceCalculatedEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback<PriceCalculatedEvent, CancellationToken>((message, _) => publishedEvent = message)
            .ThrowsAsync(new TaskCanceledException("Publisher confirm timed out."));
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<PriceCalculatedEventV2>(),
                It.IsAny<CancellationToken>()))
            .Callback<PriceCalculatedEventV2, CancellationToken>((message, _) => publishedV2 = message)
            .Returns(Task.CompletedTask);

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
        var auditRecord = await db.AuditRecords.SingleAsync(candidate => candidate.Id == result.AuditId);
        Assert.Equal(25m, auditRecord.SetupCost);
        Assert.Equal(1.25m, auditRecord.FixedDfmSurcharge);
        Assert.NotNull(publishedEvent);
        Assert.Equal(25d, publishedEvent.Payload.Breakdown.SetupCost);
        Assert.NotNull(publishedV2);
        Assert.Equal(1.25d, publishedV2.Payload.Breakdown.FixedDfmSurcharge);
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
                    DfmSurcharge: 1.25m,
                    ComplexitySurcharge: 0m,
                    SubtotalBeforeMargin: 41.25m,
                    MinimumOrderPriceFloor: 10m)
                {
                    FixedDfmSurcharge = 1.25m
                },
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
