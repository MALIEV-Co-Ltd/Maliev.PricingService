using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Maliev.MessagingContracts.Contracts.Geometry;
using Maliev.MessagingContracts.Contracts.Pricing;
using Maliev.PricingService.Api.Consumers;
using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Api.Services.Calculators;
using Maliev.PricingService.Api.Clients;
using Maliev.PricingService.Data;
using Maliev.PricingService.Data.Entities;
using Maliev.PricingService.Tests.TestFixtures;
using Moq;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
        var materialId = Guid.Empty;

        var materialClientMock = new Mock<IMaterialServiceClient>();
        materialClientMock.Setup(x => x.GetMaterialAsync(materialId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MaterialDto
            {
                Id = materialId,
                DensityGramPerCm3 = 1.24m,
                CostPerKg = 600,
                ProcessParameters = new Dictionary<string, string>()
            });

        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<FileAnalyzedEventConsumer>();
            })
            .AddScoped<Maliev.PricingService.Api.Interfaces.IPricingOrchestrator, Maliev.PricingService.Api.Services.PricingOrchestrator>()
            .AddScoped<Maliev.PricingService.Api.Services.IPricingEngine, Maliev.PricingService.Api.Services.RuleBasedPricingEngine>()
            .AddScoped<IPricingCalculator, FdmPricingCalculator>()
            .AddSingleton(materialClientMock.Object)
            .AddSingleton(Options.Create(new MachineRates()))
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
            VolumeCm3 = 10.0m,
            SupportVolumeCm3 = 2.0m,
            SurfaceAreaCm2 = 50.0m,
            BoundingBoxX = 10.0m,
            BoundingBoxY = 10.0m,
            BoundingBoxZ = 10.0m,
            IsManifold = true,
            TriangleCount = 1000
        };

        // Act
        await harness.Bus.Publish(message);

        // Assert
        Assert.True(await harness.Consumed.Any<FileAnalyzedEvent>());
        Assert.True(await harness.Published.Any<PriceCalculatedEvent>());

        // Verify Audit Record
        var db = provider.GetRequiredService<PricingDbContext>();
        var audit = db.PricingAuditRecords.FirstOrDefault(x => x.FileId == fileId);
        Assert.NotNull(audit);
        Assert.Equal("Fdm", audit.Technology);
    }
}
