using Maliev.MessagingContracts.Contracts.Orders;
using Maliev.MessagingContracts.Contracts.Shared;
using Maliev.PricingService.Api.Consumers;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Domain.Entities;
using Maliev.PricingService.Domain.Enums;
using Maliev.PricingService.Infrastructure.Data.SeedData;
using Maliev.PricingService.Infrastructure.Persistence;
using Maliev.PricingService.Tests.Unit;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.PricingService.Tests.Integration;

[Collection(PricingConfigurationDatabaseCollection.Name)]
public sealed class OrderCompletedEventConsumerTests
{
    private readonly PricingConfigurationDatabaseFixture _fixture;

    public OrderCompletedEventConsumerTests(PricingConfigurationDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Consume_OrderCompletedEventWithMatchingCorrelation_FinalizesAuditRecord()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var configuration = PricingCatalogSeedData.GetPricingConfigurations().First();
        configuration.CreatedAt = DateTime.UtcNow;
        var correlationId = Guid.NewGuid();
        var audit = new PricingAuditRecord
        {
            Id = Guid.NewGuid(),
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = configuration.MaterialId,
            MaterialCode = configuration.MaterialCode,
            ManufacturingProcessId = configuration.ManufacturingProcessId,
            ManufacturingProcessName = configuration.ManufacturingProcessCode,
            PricingConfigurationId = configuration.Id,
            Strategy = PricingStrategy.RuleBased,
            CurrencyCode = "THB",
            ValidFrom = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(1),
            CalculatedAt = DateTime.UtcNow,
            CalculatedBySystem = "PricingService",
            CorrelationId = correlationId.ToString(),
            CalculationDuration = TimeSpan.FromMilliseconds(25),
        };
        db.Configurations.Add(configuration);
        db.AuditRecords.Add(audit);
        await db.SaveChangesAsync();

        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddDbContext<PricingDbContext>(options => options.UseNpgsql(_fixture.ConnectionString))
            .AddScoped<IPricingDbContext>(services => services.GetRequiredService<PricingDbContext>())
            .AddMassTransitTestHarness(bus => bus.AddConsumer<OrderCompletedEventConsumer>())
            .BuildServiceProvider(true);
        var harness = provider.GetTestHarness();
        await harness.Start();

        await harness.Bus.Publish(CreateOrderCompletedEvent(correlationId));

        Assert.True(await harness.Consumed.Any<OrderCompletedEvent>());
        Assert.True(await harness.GetConsumerHarness<OrderCompletedEventConsumer>().Consumed.Any<OrderCompletedEvent>());
        await db.Entry(audit).ReloadAsync();
        Assert.Equal("PricingService-Finalized", audit.CalculatedBySystem);
    }

    private static OrderCompletedEvent CreateOrderCompletedEvent(Guid correlationId)
    {
        var now = DateTimeOffset.UtcNow;
        return new OrderCompletedEvent(
            Guid.NewGuid(),
            nameof(OrderCompletedEvent),
            MessageType.Event,
            "1.0.0",
            "OrderService",
            ["PricingService"],
            correlationId,
            null,
            now,
            false,
            new OrderCompletedEventPayload(
                Guid.NewGuid(),
                "ORD-TEST-001",
                Guid.NewGuid(),
                Guid.NewGuid(),
                now.AddDays(-1),
                now,
                Guid.NewGuid(),
                true,
                null,
                null,
                null,
                null,
                []));
    }
}
