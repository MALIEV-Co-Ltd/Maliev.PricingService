using System.Net;
using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.MessagingContracts.Contracts.Pricing;
using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Application.Services;
using Maliev.PricingService.Domain.Entities;
using Maliev.PricingService.Infrastructure.Data.SeedData;
using Maliev.PricingService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace Maliev.PricingService.Tests.Unit;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PricingConfigurationDatabaseCollection : ICollectionFixture<PricingConfigurationDatabaseFixture>
{
    public const string Name = "Pricing configuration database";
}

public sealed class PricingConfigurationDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .WithDatabase("pricing_configuration_tests")
        .Build();

    public string ConnectionString => _postgresContainer.GetConnectionString();

    public Task InitializeAsync() => _postgresContainer.StartAsync();

    public Task DisposeAsync() => _postgresContainer.DisposeAsync().AsTask();

    public async Task<PricingDbContext> CreateCleanDbContextAsync()
    {
        var db = new PricingDbContext(
            new DbContextOptionsBuilder<PricingDbContext>()
                .UseNpgsql(_postgresContainer.GetConnectionString())
                .Options);

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}

[Collection(PricingConfigurationDatabaseCollection.Name)]
public sealed class PricingOrchestratorQuantityTests
{
    private readonly PricingConfigurationDatabaseFixture _fixture;

    public PricingOrchestratorQuantityTests(PricingConfigurationDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(1, 500, 500)]
    [InlineData(5, 100, 500)]
    public async Task CalculatePriceAsync_RawLineTotalBelowMinimum_AppliesMinimumToLineTotal(
        int quantity,
        decimal expectedUnitPrice,
        decimal expectedTotal)
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        db.Configurations.Add(CreatePricingConfiguration(materialId, processId));
        SeedMachineCapacity(db);
        await db.SaveChangesAsync();
        var orchestrator = CreateOrchestrator(db, subtotalBeforeMargin: 80m, minimumOrderPriceFloor: 500m);

        var result = await orchestrator.CalculatePriceAsync(CreateRequest(materialId, processId, quantity));

        Assert.Equal(expectedUnitPrice, result.UnitPrice);
        Assert.Equal(expectedTotal, result.TotalAmount);
    }

    [Fact]
    public async Task CalculatePriceAsync_RawLineTotalAboveMinimum_DoesNotApplyFloor()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        db.Configurations.Add(CreatePricingConfiguration(materialId, processId));
        SeedMachineCapacity(db);
        await db.SaveChangesAsync();
        var orchestrator = CreateOrchestrator(db, subtotalBeforeMargin: 200m, minimumOrderPriceFloor: 500m);

        var result = await orchestrator.CalculatePriceAsync(CreateRequest(materialId, processId, quantity: 5));

        Assert.Equal(200m, result.UnitPrice);
        Assert.Equal(1000m, result.TotalAmount);
    }

    [Fact]
    public async Task CalculatePriceAsync_FixedSetupCost_AppliesOnceToTheCompletedLine()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        db.Configurations.Add(CreatePricingConfiguration(materialId, processId));
        SeedMachineCapacity(db);
        await db.SaveChangesAsync();
        var engine = CreatePricingEngine(
            subtotalBeforeMargin: 200m,
            minimumOrderPriceFloor: 0m,
            setupCost: 100m);
        var orchestrator = CreateOrchestrator(db, engine.Object);

        var quantityOne = await orchestrator.CalculatePriceAsync(
            CreateRequest(materialId, processId, quantity: 1));
        var quantityFive = await orchestrator.CalculatePriceAsync(
            CreateRequest(materialId, processId, quantity: 5));

        Assert.Equal(200m, quantityOne.TotalAmount);
        Assert.Equal(600m, quantityFive.TotalAmount);
        Assert.Equal(120m, quantityFive.UnitPrice);
        Assert.True(quantityFive.UnitPrice <= quantityOne.UnitPrice);
    }

    [Fact]
    public async Task CalculatePriceAsync_LeadTimeAndTolerance_ApplyToFixedSetupOnce()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        db.Configurations.Add(CreatePricingConfiguration(materialId, processId));
        db.LeadTimeOptions.Add(new LeadTimeOption
        {
            Id = Guid.NewGuid(),
            Code = "EXPRESS",
            Name = "Express",
            MinBusinessDays = 2,
            MaxBusinessDays = 3,
            PriceMultiplier = 1.3m,
            IsActive = true,
            SortOrder = 1,
        });
        SeedMachineCapacity(db);
        await db.SaveChangesAsync();
        var engine = CreatePricingEngine(
            subtotalBeforeMargin: 200m,
            minimumOrderPriceFloor: 0m,
            setupCost: 100m);
        var orchestrator = CreateOrchestrator(db, engine.Object);
        var request = CreateRequest(materialId, processId, quantity: 5) with
        {
            LeadTimeCode = "EXPRESS",
            ToleranceAdditionalCostPercent = 10m,
        };

        var result = await orchestrator.CalculatePriceAsync(request);

        Assert.Equal(858m, result.TotalAmount);
        Assert.Equal(171.6m, result.UnitPrice);
    }

    [Fact]
    public async Task CalculatePriceAsync_SeededFdmPlaConfiguration_ChargesSetupOnceAndDiscountsOnlyVariableCost()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var configuration = PricingCatalogSeedData.GetPricingConfigurations()
            .Single(candidate =>
                candidate.MaterialCode == "PLA" &&
                candidate.ManufacturingProcessCode == "FDM");
        db.Configurations.Add(configuration);
        db.VolumeDiscountTiers.AddRange(PricingCatalogSeedData.GetVolumeDiscountTiers());
        db.MachineCapacityConfigs.AddRange(PricingCatalogSeedData.GetMachineCapacityConfigs());
        await db.SaveChangesAsync();

        var engine = new RuleBasedPricingEngine(
            NullLogger<RuleBasedPricingEngine>.Instance,
            new PricingCalculatorRegistry([new FdmPricingCalculator()]));
        var orchestrator = CreateOrchestrator(db, engine);
        var oneRequest = CreateRequest(configuration.MaterialId, configuration.ManufacturingProcessId, quantity: 1) with
        {
            Dfm = new DfmMetrics { ThinWallCount = 1 },
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 1_000m,
                BoundingBoxZ = 100m,
                IsManifold = true,
            },
        };
        var fiveRequest = oneRequest with { Quantity = 5m };
        var breakdown = (await engine.CalculateAsync(oneRequest, configuration, CancellationToken.None)).Breakdown;
        var fixedLineCost = breakdown.SetupCost + breakdown.FixedDfmSurcharge;
        var variableCost = breakdown.SubtotalBeforeMargin - fixedLineCost;
        var fixedSetupWithMargin = fixedLineCost * configuration.MarginMultiplier;
        Assert.Equal(configuration.SetupCostFlat * 0.05m, breakdown.FixedDfmSurcharge);
        var expectedOne = Math.Max(
            variableCost * configuration.MarginMultiplier + fixedSetupWithMargin,
            breakdown.MinimumOrderPriceFloor);
        var expectedFive = Math.Max(
            variableCost * configuration.MarginMultiplier * 0.95m * 5m + fixedSetupWithMargin,
            breakdown.MinimumOrderPriceFloor);

        var quantityOne = await orchestrator.CalculatePriceAsync(oneRequest);
        var quantityFive = await orchestrator.CalculatePriceAsync(fiveRequest);

        Assert.Equal(expectedOne, quantityOne.TotalAmount);
        Assert.Equal(expectedFive, quantityFive.TotalAmount);
        Assert.Equal(quantityFive.TotalAmount / fiveRequest.Quantity, quantityFive.UnitPrice);
        Assert.True(quantityFive.UnitPrice <= quantityOne.UnitPrice);

        var quantityFiveAudit = await db.AuditRecords
            .SingleAsync(candidate => candidate.Id == quantityFive.AuditId);
        Assert.Equal(breakdown.SetupCost, quantityFiveAudit.SetupCost);
        Assert.Equal(breakdown.FixedDfmSurcharge, quantityFiveAudit.FixedDfmSurcharge);
        var reconstructedTotal = Math.Max(
            (quantityFiveAudit.MaterialCost + quantityFiveAudit.SupportMaterialCost + quantityFiveAudit.MachineTimeCost +
             quantityFiveAudit.VariableDfmSurcharge + quantityFiveAudit.ComplexitySurcharge) *
            configuration.MarginMultiplier * 0.95m * fiveRequest.Quantity +
            (quantityFiveAudit.SetupCost + quantityFiveAudit.FixedDfmSurcharge) * configuration.MarginMultiplier,
            quantityFiveAudit.MinimumOrderPriceFloorThb);
        Assert.Equal(quantityFiveAudit.TotalPrice, reconstructedTotal);
        Assert.Equal(breakdown.SubtotalBeforeMargin, quantityFiveAudit.SubtotalBeforeMargin);
        Assert.Equal(
            (variableCost * (configuration.MarginMultiplier - 1m)) +
            (fixedLineCost * (configuration.MarginMultiplier - 1m) / fiveRequest.Quantity),
            quantityFiveAudit.MarginAmount);
        Assert.Equal(variableCost * fiveRequest.Quantity + fixedLineCost, quantityFiveAudit.LineSubtotalBeforeMarginThb);
        Assert.Equal(
            quantityFiveAudit.LineSubtotalBeforeMarginThb * (configuration.MarginMultiplier - 1m),
            quantityFiveAudit.LineMarginAmountThb);
    }

    [Fact]
    public async Task CalculatePriceAsync_SeededFdmV2_ReconstructsCommercialLineAcrossQuantityAndFx()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var configuration = PricingCatalogSeedData.GetPricingConfigurations()
            .Single(candidate => candidate.MaterialCode == "PLA" && candidate.ManufacturingProcessCode == "FDM");
        configuration.MinimumOrderPrice = 1_000m;
        db.Configurations.Add(configuration);
        db.VolumeDiscountTiers.AddRange(PricingCatalogSeedData.GetVolumeDiscountTiers());
        db.LeadTimeOptions.AddRange(PricingCatalogSeedData.GetLeadTimeOptions());
        db.MachineCapacityConfigs.AddRange(PricingCatalogSeedData.GetMachineCapacityConfigs());
        await db.SaveChangesAsync();

        PriceCalculatedEventV2? publishedV2 = null;
        var publishEndpoint = new Mock<IPublishEndpoint>();
        publishEndpoint
            .Setup(endpoint => endpoint.Publish(
                It.IsAny<PriceCalculatedEventV2>(),
                It.IsAny<CancellationToken>()))
            .Callback<PriceCalculatedEventV2, CancellationToken>((message, _) => publishedV2 = message)
            .Returns(Task.CompletedTask);
        var engine = new RuleBasedPricingEngine(
            NullLogger<RuleBasedPricingEngine>.Instance,
            new PricingCalculatorRegistry([new FdmPricingCalculator()]));
        var orchestrator = CreateOrchestrator(db, engine, exchangeRate: 2m, publishEndpoint: publishEndpoint.Object);
        var request = CreateRequest(configuration.MaterialId, configuration.ManufacturingProcessId, quantity: 5) with
        {
            Currency = "USD",
            LeadTimeCode = "EXPRESS",
            ToleranceCode = "IT7",
            ToleranceAdditionalCostPercent = 10m,
            Dfm = new DfmMetrics { ThinWallCount = 1 },
        };

        var result = await orchestrator.CalculatePriceAsync(request);
        var audit = await db.AuditRecords.SingleAsync(candidate => candidate.Id == result.AuditId);
        Assert.NotNull(publishedV2);
        var pricing = publishedV2.Payload.Breakdown;

        Assert.True(pricing.VariableDfmSurcharge > 0d);
        Assert.True(pricing.FixedDfmSurcharge > 0d);
        Assert.Equal(5d, pricing.VolumeDiscountPercent);
        Assert.Equal(1.3d, pricing.LeadTimeMultiplier);
        Assert.Equal(1.1d, pricing.ToleranceMultiplier);
        Assert.Equal(1_000d, pricing.MinimumOrderPriceFloorThb);
        Assert.Equal(2d, pricing.ExchangeRate);
        Assert.Equal("THB", pricing.BaseCurrency);

        var variableUnitThb = pricing.MaterialCost + pricing.SupportCost + pricing.MachineTimeCost +
                              pricing.VariableDfmSurcharge + pricing.ComplexitySurcharge;
        var reconstructedLineSubtotal = variableUnitThb * publishedV2.Payload.Quantity +
                                        pricing.SetupCost + pricing.FixedDfmSurcharge;
        var reconstructedLineMargin = reconstructedLineSubtotal * (pricing.MarginMultiplier - 1d);
        var discountedVariableLine = variableUnitThb * pricing.MarginMultiplier *
                                     (1d - pricing.VolumeDiscountPercent / 100d) * publishedV2.Payload.Quantity;
        var marginedFixedLine = (pricing.SetupCost + pricing.FixedDfmSurcharge) * pricing.MarginMultiplier;
        var surchargedLineThb = (discountedVariableLine + marginedFixedLine) *
                                pricing.LeadTimeMultiplier * pricing.ToleranceMultiplier;
        var reconstructedTotal = Math.Max(surchargedLineThb, pricing.MinimumOrderPriceFloorThb) * pricing.ExchangeRate;

        Assert.True(surchargedLineThb < pricing.MinimumOrderPriceFloorThb);
        Assert.Equal(pricing.MinimumOrderPriceFloorThb * pricing.ExchangeRate, reconstructedTotal, precision: 8);
        Assert.Equal(reconstructedLineSubtotal, pricing.SubtotalBeforeMargin, precision: 8);
        Assert.Equal(reconstructedLineMargin, pricing.MarginAmount, precision: 8);
        Assert.Equal(reconstructedTotal, pricing.TotalPrice, precision: 8);
        Assert.Equal(reconstructedTotal, publishedV2.Payload.TotalPrice, precision: 8);
        Assert.Equal(reconstructedTotal / publishedV2.Payload.Quantity, publishedV2.Payload.TotalUnitPrice, precision: 8);
        Assert.Equal(result.TotalAmount, (decimal)reconstructedTotal);

        Assert.Equal(pricing.VariableDfmSurcharge, (double)audit.VariableDfmSurcharge, precision: 8);
        Assert.Equal(pricing.LeadTimeMultiplier, (double)audit.LeadTimeMultiplier, precision: 8);
        Assert.Equal(pricing.ToleranceMultiplier, (double)audit.ToleranceMultiplier, precision: 8);
        Assert.Equal(pricing.MinimumOrderPriceFloorThb, (double)audit.MinimumOrderPriceFloorThb, precision: 8);
        Assert.Equal(pricing.SubtotalBeforeMargin, (double)audit.LineSubtotalBeforeMarginThb, precision: 8);
        Assert.Equal(pricing.MarginAmount, (double)audit.LineMarginAmountThb, precision: 8);
    }

    [Fact]
    public async Task CalculatePriceAsync_FloorFxAndDiscount_DerivesConsistentCustomerCurrencyAmounts()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        db.Configurations.Add(CreatePricingConfiguration(materialId, processId));
        db.VolumeDiscountTiers.Add(new VolumeDiscountTier
        {
            Id = Guid.NewGuid(),
            MinQuantity = 5,
            MaxQuantity = 5,
            DiscountPercent = 10m,
            IsActive = true,
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow,
        });
        SeedMachineCapacity(db);
        await db.SaveChangesAsync();
        var engine = CreatePricingEngine(subtotalBeforeMargin: 110m, minimumOrderPriceFloor: 500m);
        var orchestrator = CreateOrchestrator(db, engine.Object, exchangeRate: 2m);
        var request = CreateRequest(materialId, processId, quantity: 5) with { Currency = "USD" };

        var result = await orchestrator.CalculatePriceAsync(request);

        Assert.Equal(200m, result.UnitPrice);
        Assert.Equal(1000m, result.TotalAmount);
        Assert.Equal(result.UnitPrice * request.Quantity, result.TotalAmount);
        Assert.Equal(220m, result.UnitPriceBeforeVolumeDiscount);
        Assert.Equal(20m, result.VolumeDiscountUnitAmount);
        Assert.Equal(10m, result.VolumeDiscountPercent);
    }

    [Fact]
    public async Task CalculatePriceAsync_JobTokenExchangeFailure_StopsPricingWorkflow()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        db.Configurations.Add(CreatePricingConfiguration(materialId, processId));
        SeedMachineCapacity(db);
        await db.SaveChangesAsync();
        var jobServiceClient = new Mock<IJobServiceClient>();
        jobServiceClient
            .Setup(client => client.GetQueueDepthByTechnologyAsync(
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ServiceTokenExchangeException("AuthService unavailable."));
        var orchestrator = CreateOrchestrator(
            db,
            CreatePricingEngine(subtotalBeforeMargin: 100m, minimumOrderPriceFloor: 0m).Object,
            jobServiceClient: jobServiceClient.Object);

        await Assert.ThrowsAsync<ServiceTokenExchangeException>(
            () => orchestrator.CalculatePriceAsync(CreateRequest(materialId, processId, quantity: 1)));

        Assert.Empty(db.AuditRecords);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CalculatePriceAsync_JobAuthorizationFailure_StopsPricingWorkflow(HttpStatusCode statusCode)
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        db.Configurations.Add(CreatePricingConfiguration(materialId, processId));
        SeedMachineCapacity(db);
        await db.SaveChangesAsync();
        var jobServiceClient = new Mock<IJobServiceClient>();
        jobServiceClient
            .Setup(client => client.GetQueueDepthByTechnologyAsync(
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("JobService denied the workload.", null, statusCode));
        var orchestrator = CreateOrchestrator(
            db,
            CreatePricingEngine(subtotalBeforeMargin: 100m, minimumOrderPriceFloor: 0m).Object,
            jobServiceClient: jobServiceClient.Object);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => orchestrator.CalculatePriceAsync(CreateRequest(materialId, processId, quantity: 1)));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.Empty(db.AuditRecords);
    }

    [Fact]
    public async Task CalculatePriceAsync_JobCancellation_StopsPricingWorkflow()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        db.Configurations.Add(CreatePricingConfiguration(materialId, processId));
        SeedMachineCapacity(db);
        await db.SaveChangesAsync();
        var jobServiceClient = new Mock<IJobServiceClient>();
        jobServiceClient
            .Setup(client => client.GetQueueDepthByTechnologyAsync(
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("JobService request was canceled."));
        var orchestrator = CreateOrchestrator(
            db,
            CreatePricingEngine(subtotalBeforeMargin: 100m, minimumOrderPriceFloor: 0m).Object,
            jobServiceClient: jobServiceClient.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => orchestrator.CalculatePriceAsync(CreateRequest(materialId, processId, quantity: 1)));

        Assert.Empty(db.AuditRecords);
    }

    [Fact]
    public async Task CalculatePriceAsync_ExactActiveIdMatch_UsesExactConfigurationBeforeCodeFallback()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        var exactConfiguration = CreatePricingConfiguration(
            materialId,
            processId,
            materialCode: "ABS",
            manufacturingProcessCode: "SLA_DLP");
        var fallbackConfiguration = CreatePricingConfiguration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            materialCode: "PLA",
            manufacturingProcessCode: "FDM");
        db.Configurations.AddRange(exactConfiguration, fallbackConfiguration);
        SeedMachineCapacity(db);
        await db.SaveChangesAsync();
        PricingConfiguration? selectedConfiguration = null;
        var engine = CreatePricingEngine(
            subtotalBeforeMargin: 100m,
            minimumOrderPriceFloor: 0m,
            configuration => selectedConfiguration = configuration);
        var orchestrator = CreateOrchestrator(db, engine.Object);

        var result = await orchestrator.CalculatePriceAsync(CreateRequest(materialId, processId, quantity: 1));

        Assert.NotEqual(Guid.Empty, result.AuditId);
        Assert.NotNull(selectedConfiguration);
        Assert.Equal(exactConfiguration.Id, selectedConfiguration.Id);
    }

    [Fact]
    public async Task CalculatePriceAsync_IdsDriftAndCodesMatch_UsesSingleActiveCodeConfiguration()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var fallbackConfiguration = CreatePricingConfiguration(
            Guid.NewGuid(),
            Guid.NewGuid(),
            materialCode: "PLA",
            manufacturingProcessCode: "FDM");
        db.Configurations.Add(fallbackConfiguration);
        SeedMachineCapacity(db);
        await db.SaveChangesAsync();
        PricingConfiguration? selectedConfiguration = null;
        var engine = CreatePricingEngine(
            subtotalBeforeMargin: 100m,
            minimumOrderPriceFloor: 0m,
            configuration => selectedConfiguration = configuration);
        var logger = new Mock<ILogger<PricingOrchestrator>>();
        var orchestrator = CreateOrchestrator(db, engine.Object, logger.Object);
        var request = CreateRequest(Guid.NewGuid(), Guid.NewGuid(), quantity: 1) with
        {
            MaterialCode = "  pla  ",
            ManufacturingProcessName = "Fused Filament Fabrication (FDM)",
        };

        var result = await orchestrator.CalculatePriceAsync(request);

        Assert.NotEqual(Guid.Empty, result.AuditId);
        Assert.NotNull(selectedConfiguration);
        Assert.Equal(fallbackConfiguration.Id, selectedConfiguration.Id);
        VerifyLogContains(logger, LogLevel.Information, "stable codes");
    }

    [Fact]
    public async Task CalculatePriceAsync_IdsDriftAndCodeMatchIsAmbiguous_ReturnsNoPrice()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        db.Configurations.AddRange(
            CreatePricingConfiguration(
                Guid.NewGuid(),
                Guid.NewGuid(),
                materialCode: "PLA",
                manufacturingProcessCode: "FDM"),
            CreatePricingConfiguration(
                Guid.NewGuid(),
                Guid.NewGuid(),
                materialCode: "PLA",
                manufacturingProcessCode: "FDM"));
        await db.SaveChangesAsync();
        var engine = CreatePricingEngine(subtotalBeforeMargin: 100m, minimumOrderPriceFloor: 0m);
        var orchestrator = CreateOrchestrator(db, engine.Object);

        var result = await orchestrator.CalculatePriceAsync(CreateRequest(Guid.NewGuid(), Guid.NewGuid(), quantity: 1));

        Assert.Equal(0m, result.UnitPrice);
        Assert.Equal(0m, result.TotalAmount);
        Assert.Equal(Guid.Empty, result.AuditId);
        engine.Verify(candidate => candidate.CalculateAsync(
            It.IsAny<PricingRequest>(),
            It.IsAny<PricingConfiguration>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PricingOrchestrator CreateOrchestrator(
        PricingDbContext db,
        decimal subtotalBeforeMargin,
        decimal minimumOrderPriceFloor)
        => CreateOrchestrator(
            db,
            CreatePricingEngine(subtotalBeforeMargin, minimumOrderPriceFloor).Object);

    private static PricingOrchestrator CreateOrchestrator(
        PricingDbContext db,
        IPricingEngine engine,
        ILogger<PricingOrchestrator>? logger = null,
        decimal exchangeRate = 1m,
        IJobServiceClient? jobServiceClient = null,
        IPublishEndpoint? publishEndpoint = null)
    {
        if (jobServiceClient is null)
        {
            var defaultJobServiceClient = new Mock<IJobServiceClient>();
            defaultJobServiceClient
                .Setup(client => client.GetQueueDepthByTechnologyAsync(
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
            jobServiceClient = defaultJobServiceClient.Object;
        }

        var defaultPublishEndpoint = new Mock<IPublishEndpoint>();
        var currencyClient = new Mock<ICurrencyServiceClient>();
        currencyClient
            .Setup(client => client.GetExchangeRateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(exchangeRate);

        return new PricingOrchestrator(
            db,
            engine,
            jobServiceClient,
            logger ?? NullLogger<PricingOrchestrator>.Instance,
            publishEndpoint ?? defaultPublishEndpoint.Object,
            new VolumeDiscountResolver(db),
            currencyClient.Object);
    }

    private static Mock<IPricingEngine> CreatePricingEngine(
        decimal subtotalBeforeMargin,
        decimal minimumOrderPriceFloor,
        Action<PricingConfiguration>? onConfigurationSelected = null,
        decimal setupCost = 0m)
    {
        var engine = new Mock<IPricingEngine>();
        engine
            .Setup(candidate => candidate.CalculateAsync(
                It.IsAny<PricingRequest>(),
                It.IsAny<PricingConfiguration>(),
                It.IsAny<CancellationToken>()))
            .Callback<PricingRequest, PricingConfiguration, CancellationToken>(
                (_, configuration, _) => onConfigurationSelected?.Invoke(configuration))
            .ReturnsAsync(new EngineResult(
                new CostBreakdown(
                    MaterialCost: subtotalBeforeMargin - setupCost,
                    SupportMaterialCost: 0m,
                    MachineTimeCost: 0m,
                    SetupCost: setupCost,
                    DfmSurcharge: 0m,
                    ComplexitySurcharge: 0m,
                    SubtotalBeforeMargin: subtotalBeforeMargin,
                    MinimumOrderPriceFloor: minimumOrderPriceFloor),
                "TestEngine"));
        return engine;
    }

    private static PricingConfiguration CreatePricingConfiguration(
        Guid materialId,
        Guid processId,
        string materialCode = "PLA",
        string manufacturingProcessCode = "FDM") => new()
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            MaterialCode = materialCode,
            ManufacturingProcessId = processId,
            ManufacturingProcessCode = manufacturingProcessCode,
            MaterialPricePerCm3 = 1m,
            SupportMaterialPricePerCm3 = 0m,
            MachineHourlyRate = 100m,
            PrintSpeedCm3PerHour = 10m,
            SetupCostFlat = 0m,
            MinimumOrderPrice = 500m,
            MarginMultiplier = 1m,
            ComplexityThreshold = 6m,
            ComplexitySurchargePercent = 0m,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };

    private static void SeedMachineCapacity(PricingDbContext db)
    {
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
    }

    private static void VerifyLogContains(
        Mock<ILogger<PricingOrchestrator>> logger,
        LogLevel level,
        string expectedText)
    {
        logger.Verify(candidate => candidate.Log(
            level,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains(expectedText, StringComparison.OrdinalIgnoreCase)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    private static PricingRequest CreateRequest(Guid materialId, Guid processId, int quantity) => new()
    {
        FileId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        MaterialId = materialId,
        MaterialCode = "PLA",
        ManufacturingProcessId = processId,
        ManufacturingProcessName = "FDM",
        Quantity = quantity,
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
        StoragePath = "projects/quantity-test.stl",
    };
}
