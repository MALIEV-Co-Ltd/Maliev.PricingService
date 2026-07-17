using Maliev.PricingService.Infrastructure.Data.SeedData;
using Maliev.PricingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Maliev.PricingService.Tests.Integration;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PricingMigrationDatabaseCollection : ICollectionFixture<PricingMigrationDatabaseFixture>
{
    public const string Name = "Pricing migration database";
}

public sealed class PricingMigrationDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .WithDatabase("pricing_migration_tests")
        .Build();

    public Task InitializeAsync() => _postgresContainer.StartAsync();

    public Task DisposeAsync() => _postgresContainer.DisposeAsync().AsTask();

    public async Task<PricingDbContext> CreateEmptyDbContextAsync()
    {
        var db = new PricingDbContext(
            new DbContextOptionsBuilder<PricingDbContext>()
                .UseNpgsql(_postgresContainer.GetConnectionString())
                .Options);

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await db.Database.EnsureDeletedAsync();
        return db;
    }
}

[Collection(PricingMigrationDatabaseCollection.Name)]
public sealed class PricingMigrationReadinessTests
{
    private const string PreStableCodesMigration = "20260612054059_AddMassTransitOutbox";
    private readonly PricingMigrationDatabaseFixture _fixture;

    public PricingMigrationReadinessTests(PricingMigrationDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MigrateAsync_EmptyPostgreSqlDatabase_AppliesFullHistoryWithoutUserDefinedXminColumns()
    {
        await using var db = await _fixture.CreateEmptyDbContextAsync();

        await db.Database.MigrateAsync();

        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name IN ('machine_capacity_configs', 'pricing_configurations', 'pricing_snapshots')
              AND column_name = 'xmin';
            """;

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
        Assert.Equal(
            db.Database.GetMigrations().Count(),
            (await db.Database.GetAppliedMigrationsAsync()).Count());
    }

    [Fact]
    public async Task MigrateAsync_PreStableCodeCatalogRows_BackfillsEveryStableCodeWithoutChangingCommercialValues()
    {
        await using var db = await _fixture.CreateEmptyDbContextAsync();
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(PreStableCodesMigration);
        var catalog = PricingCatalogSeedData.GetPricingConfigurations().ToList();
        var editedCatalog = catalog.Single(configuration =>
            configuration.MaterialCode == "PLA"
            && configuration.ManufacturingProcessCode == "FDM");

        foreach (var configuration in catalog)
        {
            var isEdited = configuration.Id == editedCatalog.Id;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO pricing_configurations
                    ("Id", "MaterialId", "ManufacturingProcessId", "MaterialPricePerCm3",
                     "SupportMaterialPricePerCm3", "MachineHourlyRate", "PrintSpeedCm3PerHour",
                     "DensityGramPerCm3", "SetupCostFlat", "MinimumOrderPrice", "MarginMultiplier",
                     "ComplexityThreshold", "ComplexitySurchargePercent", "EffectiveFrom", "EffectiveTo",
                     "IsActive", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy")
                VALUES
                    ({configuration.Id}, {configuration.MaterialId}, {configuration.ManufacturingProcessId},
                     {(isEdited ? 91m : configuration.MaterialPricePerCm3)},
                     {(isEdited ? 92m : configuration.SupportMaterialPricePerCm3)},
                     {(isEdited ? 930m : configuration.MachineHourlyRate)},
                     {(isEdited ? 94m : configuration.PrintSpeedCm3PerHour)},
                     {(isEdited ? 9.5m : configuration.DensityGramPerCm3)},
                     {(isEdited ? 960m : configuration.SetupCostFlat)},
                     {(isEdited ? 970m : configuration.MinimumOrderPrice)},
                     {(isEdited ? 3.8m : configuration.MarginMultiplier)},
                     {(isEdited ? 9.9m : configuration.ComplexityThreshold)},
                     {(isEdited ? 41m : configuration.ComplexitySurchargePercent)},
                     {configuration.EffectiveFrom}, {configuration.EffectiveTo},
                     {(isEdited ? false : configuration.IsActive)},
                     {new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)}, {"administrator"},
                     {new DateTime(2026, 6, 7, 0, 0, 0, DateTimeKind.Utc)}, {"administrator"});
                """);
        }

        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        var migratedCatalog = await db.Configurations
            .Where(configuration => catalog.Select(expected => expected.Id).Contains(configuration.Id))
            .ToDictionaryAsync(configuration => configuration.Id);
        var migrated = migratedCatalog[editedCatalog.Id];

        Assert.Equal(catalog.Count, migratedCatalog.Count);
        Assert.All(catalog, expected =>
        {
            Assert.Equal(expected.MaterialCode, migratedCatalog[expected.Id].MaterialCode);
            Assert.Equal(expected.ManufacturingProcessCode, migratedCatalog[expected.Id].ManufacturingProcessCode);
        });
        Assert.Equal(91m, migrated.MaterialPricePerCm3);
        Assert.Equal(92m, migrated.SupportMaterialPricePerCm3);
        Assert.Equal(930m, migrated.MachineHourlyRate);
        Assert.Equal(94m, migrated.PrintSpeedCm3PerHour);
        Assert.Equal(9.5m, migrated.DensityGramPerCm3);
        Assert.Equal(960m, migrated.SetupCostFlat);
        Assert.Equal(970m, migrated.MinimumOrderPrice);
        Assert.Equal(3.8m, migrated.MarginMultiplier);
        Assert.Equal(9.9m, migrated.ComplexityThreshold);
        Assert.Equal(41m, migrated.ComplexitySurchargePercent);
        Assert.False(migrated.IsActive);
        Assert.Equal("administrator", migrated.UpdatedBy);
    }

    [Fact]
    public async Task MigrateAsync_PreStableCodeUnknownRow_FailsWithoutCommittingEmptyStableCodes()
    {
        await using var db = await _fixture.CreateEmptyDbContextAsync();
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync(PreStableCodesMigration);
        var unknownId = Guid.NewGuid();

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO pricing_configurations
                ("Id", "MaterialId", "ManufacturingProcessId", "MaterialPricePerCm3",
                 "SupportMaterialPricePerCm3", "MachineHourlyRate", "PrintSpeedCm3PerHour",
                 "DensityGramPerCm3", "SetupCostFlat", "MinimumOrderPrice", "MarginMultiplier",
                 "ComplexityThreshold", "ComplexitySurchargePercent", "EffectiveFrom", "EffectiveTo",
                 "IsActive", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy")
            VALUES
                ({unknownId}, {Guid.NewGuid()}, {Guid.NewGuid()}, {11m}, {12m}, {130m}, {14m},
                 {1.2m}, {150m}, {160m}, {1.7m}, {6m}, {15m},
                 {new DateTime(2024, 2, 3, 0, 0, 0, DateTimeKind.Utc)}, {null}, {true},
                 {new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)}, {"administrator"},
                 {null}, {null});
            """);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());

        Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
        Assert.Contains("stable code", exception.MessageText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "20260710024047_AddPricingConfigurationStableCodes",
            await db.Database.GetAppliedMigrationsAsync());
    }
}
