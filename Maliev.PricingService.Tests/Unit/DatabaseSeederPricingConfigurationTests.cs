using Maliev.PricingService.Domain.Entities;
using Maliev.PricingService.Infrastructure.Data.SeedData;
using Maliev.PricingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Maliev.PricingService.Tests.Unit;

public sealed class PricingCatalogSeedDataTests
{
    [Fact]
    public void GetPricingConfigurations_AllCatalogRowsHaveStableCodes()
    {
        var configurations = PricingCatalogSeedData.GetPricingConfigurations().ToList();

        Assert.NotEmpty(configurations);
        Assert.All(configurations, configuration =>
        {
            Assert.False(string.IsNullOrWhiteSpace(configuration.MaterialCode));
            Assert.False(string.IsNullOrWhiteSpace(configuration.ManufacturingProcessCode));
        });
    }
}

[Collection(PricingConfigurationDatabaseCollection.Name)]
public sealed class DatabaseSeederPricingConfigurationTests
{
    private readonly PricingConfigurationDatabaseFixture _fixture;

    public DatabaseSeederPricingConfigurationTests(PricingConfigurationDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SeedPricingConfigurationsAsync_ExistingCatalogRow_BackfillsCodesWithoutOverwritingCommercialEdits()
    {
        await using var db = await _fixture.CreateCleanDbContextAsync();
        var catalogConfigurations = PricingCatalogSeedData.GetPricingConfigurations().ToList();
        var plaFdmCatalogConfiguration = catalogConfigurations.Single(configuration =>
            configuration.MaterialPricePerCm3 == 0.055m
            && configuration.SupportMaterialPricePerCm3 == 0.035m
            && configuration.MachineHourlyRate == 120m
            && configuration.PrintSpeedCm3PerHour == 20m);
        var editedEffectiveFrom = new DateTime(2024, 2, 3, 0, 0, 0, DateTimeKind.Utc);
        var editedEffectiveTo = new DateTime(2030, 4, 5, 0, 0, 0, DateTimeKind.Utc);
        var editedCreatedAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var editedUpdatedAt = new DateTime(2026, 6, 7, 0, 0, 0, DateTimeKind.Utc);
        var existingCatalogRow = new PricingConfiguration
        {
            Id = plaFdmCatalogConfiguration.Id,
            MaterialId = plaFdmCatalogConfiguration.MaterialId,
            MaterialCode = string.Empty,
            ManufacturingProcessId = plaFdmCatalogConfiguration.ManufacturingProcessId,
            ManufacturingProcessCode = string.Empty,
            MaterialPricePerCm3 = 91m,
            SupportMaterialPricePerCm3 = 92m,
            MachineHourlyRate = 930m,
            PrintSpeedCm3PerHour = 94m,
            DensityGramPerCm3 = 9.5m,
            SetupCostFlat = 960m,
            MinimumOrderPrice = 970m,
            MarginMultiplier = 3.8m,
            ComplexityThreshold = 9.9m,
            ComplexitySurchargePercent = 41m,
            EffectiveFrom = editedEffectiveFrom,
            EffectiveTo = editedEffectiveTo,
            IsActive = false,
            CreatedAt = editedCreatedAt,
            CreatedBy = "administrator",
            UpdatedAt = editedUpdatedAt,
            UpdatedBy = "administrator",
        };
        var nonCatalogRow = new PricingConfiguration
        {
            Id = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "CUSTOM_MATERIAL",
            ManufacturingProcessId = Guid.NewGuid(),
            ManufacturingProcessCode = "CUSTOM_PROCESS",
            MaterialPricePerCm3 = 11m,
            SupportMaterialPricePerCm3 = 12m,
            MachineHourlyRate = 130m,
            PrintSpeedCm3PerHour = 14m,
            SetupCostFlat = 150m,
            MinimumOrderPrice = 160m,
            MarginMultiplier = 1.7m,
            ComplexityThreshold = 6m,
            ComplexitySurchargePercent = 15m,
            EffectiveFrom = editedEffectiveFrom,
            IsActive = true,
            CreatedAt = editedCreatedAt,
            CreatedBy = "administrator",
        };
        db.Configurations.AddRange(existingCatalogRow, nonCatalogRow);
        await db.SaveChangesAsync();
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Test database connection string was not available.");
        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddDbContext<PricingDbContext>(options => options.UseNpgsql(connectionString));
            })
            .Build();

        await host.SeedPricingConfigurationsAsync();

        await db.Entry(existingCatalogRow).ReloadAsync();
        await db.Entry(nonCatalogRow).ReloadAsync();
        Assert.Equal("PLA", existingCatalogRow.MaterialCode);
        Assert.Equal("FDM", existingCatalogRow.ManufacturingProcessCode);
        Assert.Equal(91m, existingCatalogRow.MaterialPricePerCm3);
        Assert.Equal(92m, existingCatalogRow.SupportMaterialPricePerCm3);
        Assert.Equal(930m, existingCatalogRow.MachineHourlyRate);
        Assert.Equal(94m, existingCatalogRow.PrintSpeedCm3PerHour);
        Assert.Equal(9.5m, existingCatalogRow.DensityGramPerCm3);
        Assert.Equal(960m, existingCatalogRow.SetupCostFlat);
        Assert.Equal(970m, existingCatalogRow.MinimumOrderPrice);
        Assert.Equal(3.8m, existingCatalogRow.MarginMultiplier);
        Assert.Equal(9.9m, existingCatalogRow.ComplexityThreshold);
        Assert.Equal(41m, existingCatalogRow.ComplexitySurchargePercent);
        Assert.Equal(editedEffectiveFrom, existingCatalogRow.EffectiveFrom);
        Assert.Equal(editedEffectiveTo, existingCatalogRow.EffectiveTo);
        Assert.False(existingCatalogRow.IsActive);
        Assert.Equal(editedCreatedAt, existingCatalogRow.CreatedAt);
        Assert.Equal("administrator", existingCatalogRow.CreatedBy);
        Assert.Equal(editedUpdatedAt, existingCatalogRow.UpdatedAt);
        Assert.Equal("administrator", existingCatalogRow.UpdatedBy);
        Assert.Equal("CUSTOM_MATERIAL", nonCatalogRow.MaterialCode);
        Assert.Equal("CUSTOM_PROCESS", nonCatalogRow.ManufacturingProcessCode);
        Assert.Equal(11m, nonCatalogRow.MaterialPricePerCm3);
        Assert.Equal(catalogConfigurations.Count + 1, await db.Configurations.CountAsync());
        Assert.All(catalogConfigurations, catalog =>
            Assert.True(db.Configurations.Any(configuration => configuration.Id == catalog.Id)));
    }
}
