using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Maliev.MessagingContracts.Contracts.Geometry;
using Maliev.MessagingContracts.Contracts.Pricing;
using Maliev.PricingService.Api.Consumers;
using Maliev.PricingService.Data;
using Maliev.PricingService.Data.Entities;
using Maliev.PricingService.Tests.TestFixtures;
using Xunit;

namespace Maliev.PricingService.Tests.Integration;

public class FileAnalyzedEventConsumerTests : IClassFixture<PricingServiceTestFactory>
{
    private readonly PricingServiceTestFactory _factory;

    public FileAnalyzedEventConsumerTests(PricingServiceTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Consume_FileAnalyzedEvent_PublishesPriceCalculatedEventAndCreatesAudit()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var materialId = Guid.Empty; // Consumer uses Guid.Empty for now
        var processId = Guid.Empty;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
            db.PricingConfigurations.Add(new PricingConfiguration
            {
                MaterialId = materialId,
                ManufacturingProcessId = processId,
                MaterialPricePerCm3 = 10.0m,
                SupportMaterialPricePerCm3 = 5.0m,
                MachineHourlyRate = 50.0m,
                PrintSpeedCm3PerHour = 100.0m,
                SetupCostFlat = 25.0m,
                MarginMultiplier = 1.2m,
                EffectiveFrom = DateTime.UtcNow.AddDays(-1),
                CreatedBy = "Test"
            });
            await db.SaveChangesAsync();
        }

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<FileAnalyzedEventConsumer>();
            })
            .AddScoped<Maliev.PricingService.Api.Interfaces.IPricingOrchestrator, Maliev.PricingService.Api.Services.PricingOrchestrator>()
            .AddScoped<Maliev.PricingService.Api.Services.IPricingEngine, Maliev.PricingService.Api.Services.RuleBasedPricingEngine>()
            .AddDbContext<PricingDbContext>(options =>
            {
                Microsoft.EntityFrameworkCore.NpgsqlDbContextOptionsBuilderExtensions.UseNpgsql(options, _factory.ConnectionString);
            })
            .AddLogging()
            .AddMemoryCache()
            .BuildServiceProvider();

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var message = new FileAnalyzedEvent
        {
            FileId = fileId,
            CustomerId = customerId,
            Volume = 10.0,
            SupportVolume = 2.0,
            SurfaceArea = 50.0,
            BoundingBoxX = 10.0,
            BoundingBoxY = 10.0,
            BoundingBoxZ = 10.0,
            IsManifold = true,
            TriangleCount = 1000
        };

        // Act
        await harness.Bus.Publish(message);

        // Assert
        Assert.True(await harness.Consumed.Any<FileAnalyzedEvent>());
        Assert.True(await harness.Published.Any<PriceCalculatedEvent>());

        // Verify Audit Record
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
            var audit = db.PricingAuditRecords.FirstOrDefault(x => x.FileId == fileId);
            Assert.NotNull(audit);
        }
    }
}
