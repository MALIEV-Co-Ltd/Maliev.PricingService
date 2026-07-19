using System.Text.Json;
using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Application.Services;
using Maliev.PricingService.Domain.Entities;
using Maliev.PricingService.Infrastructure.Persistence;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace Maliev.PricingService.Tests.Integration;

public sealed class PricingBusOutboxIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .WithDatabase("pricing_bus_outbox_tests")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task CalculatePriceAsync_CommitsAuditSnapshotAndBothEventsToEfBusOutbox()
    {
        var services = new ServiceCollection();
        services.AddDbContext<PricingDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IPricingDbContext>(provider => provider.GetRequiredService<PricingDbContext>());
        services.AddMassTransit(configurator =>
        {
            configurator.AddEntityFrameworkOutbox<PricingDbContext>(options =>
            {
                options.UsePostgres();
                options.QueryDelay = TimeSpan.FromHours(1);
                options.UseBusOutbox();
            });
            configurator.UsingInMemory((_, bus) => bus.ConfigureEndpoints(_));
        });

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        await db.Database.EnsureCreatedAsync();

        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        db.Configurations.Add(CreateConfiguration(materialId, processId));
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
        await db.SaveChangesAsync();

        var engine = new Mock<IPricingEngine>();
        engine.Setup(candidate => candidate.CalculateAsync(
                It.IsAny<PricingRequest>(),
                It.IsAny<PricingConfiguration>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EngineResult(
                new CostBreakdown(10m, 0m, 5m, 25m, 2m, 0m, 42m, 10m)
                {
                    FixedDfmSurcharge = 1.25m,
                },
                "OutboxIntegrationEngine"));
        var jobClient = new Mock<IJobServiceClient>();
        jobClient.Setup(candidate => candidate.GetQueueDepthByTechnologyAsync(
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var currencyClient = new Mock<ICurrencyServiceClient>();
        currencyClient.Setup(candidate => candidate.GetExchangeRateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1m);
        var orchestrator = new PricingOrchestrator(
            db,
            engine.Object,
            jobClient.Object,
            NullLogger<PricingOrchestrator>.Instance,
            scope.ServiceProvider.GetRequiredService<IPublishEndpoint>(),
            new VolumeDiscountResolver(db),
            currencyClient.Object);

        var result = await orchestrator.CalculatePriceAsync(CreateRequest(materialId, processId));

        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.AuditRecords.CountAsync(candidate => candidate.Id == result.AuditId));
        Assert.Equal(1, await db.Snapshots.CountAsync(candidate => candidate.PricingAuditRecordId == result.AuditId));
        var outboxMessages = await db.Set<OutboxMessage>().AsNoTracking().OrderBy(message => message.SequenceNumber).ToListAsync();
        Assert.Equal(2, outboxMessages.Count);
        Assert.Contains("PriceCalculatedEvent", outboxMessages[0].MessageType, StringComparison.Ordinal);
        Assert.Contains("PriceCalculatedEventV2", outboxMessages[1].MessageType, StringComparison.Ordinal);
        using var v1Body = JsonDocument.Parse(outboxMessages[0].Body);
        using var v2Body = JsonDocument.Parse(outboxMessages[1].Body);
        Assert.Equal("PriceCalculatedEvent", GetStringProperty(v1Body.RootElement, "messageName"));
        Assert.Equal("PriceCalculatedEventV2", GetStringProperty(v2Body.RootElement, "messageName"));
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                return property.Value.GetString();
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                var nested = GetStringProperty(property.Value, propertyName);
                if (nested is not null)
                    return nested;
            }
        }

        return null;
    }

    private static PricingConfiguration CreateConfiguration(Guid materialId, Guid processId) => new()
    {
        Id = Guid.NewGuid(),
        MaterialId = materialId,
        MaterialCode = "PLA",
        ManufacturingProcessId = processId,
        ManufacturingProcessCode = "FDM",
        MaterialPricePerCm3 = 1m,
        SupportMaterialPricePerCm3 = 0m,
        MachineHourlyRate = 100m,
        PrintSpeedCm3PerHour = 10m,
        SetupCostFlat = 25m,
        MinimumOrderPrice = 10m,
        MarginMultiplier = 1.5m,
        EffectiveFrom = DateTime.UtcNow.AddDays(-1),
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = "test",
    };

    private static PricingRequest CreateRequest(Guid materialId, Guid processId) => new()
    {
        FileId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        MaterialId = materialId,
        MaterialCode = "PLA",
        ManufacturingProcessId = processId,
        ManufacturingProcessName = "FDM",
        Quantity = 2,
        Currency = "THB",
        Geometry = new GeometryMetrics
        {
            VolumeCm3 = 10m,
            SurfaceAreaCm2 = 25m,
            BoundingBoxX = 10m,
            BoundingBoxY = 10m,
            BoundingBoxZ = 10m,
            IsManifold = true,
            TriangleCount = 100,
        },
        CorrelationId = Guid.NewGuid(),
        StoragePath = "projects/outbox-test.stl",
    };
}
