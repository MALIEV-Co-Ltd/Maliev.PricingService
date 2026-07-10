using Maliev.PricingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.PricingService.Infrastructure.Data.SeedData;

/// <summary>
/// Handles initial database seeding for pricing catalog data.
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// Seeds lead time options if the table is empty.
    /// </summary>
    public static async Task SeedLeadTimeOptionsAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        try
        {
            if (await context.LeadTimeOptions.AnyAsync())
            {
                logger.LogInformation("Lead time options already seeded. Skipping.");
                return;
            }

            var options = PricingCatalogSeedData.GetLeadTimeOptions().ToList();
            var now = DateTime.UtcNow;
            foreach (var opt in options) opt.CreatedAt = now;

            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await context.LeadTimeOptions.AddRangeAsync(options);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} lead time options.", options.Count);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding lead time options.");
        }
    }

    /// <summary>
    /// Seeds volume discount tiers if the table is empty.
    /// </summary>
    public static async Task SeedVolumeDiscountTiersAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        try
        {
            if (await context.VolumeDiscountTiers.AnyAsync())
            {
                logger.LogInformation("Volume discount tiers already seeded. Skipping.");
                return;
            }

            var tiers = PricingCatalogSeedData.GetVolumeDiscountTiers().ToList();
            var now = DateTime.UtcNow;
            foreach (var tier in tiers) tier.CreatedAt = now;

            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await context.VolumeDiscountTiers.AddRangeAsync(tiers);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} volume discount tiers.", tiers.Count);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding volume discount tiers.");
        }
    }

    /// <summary>
    /// Seeds machine capacity configs if the table is empty.
    /// </summary>
    public static async Task SeedMachineCapacityConfigsAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        try
        {
            if (await context.MachineCapacityConfigs.AnyAsync())
            {
                logger.LogInformation("Machine capacity configs already seeded. Skipping.");
                return;
            }

            var configs = PricingCatalogSeedData.GetMachineCapacityConfigs().ToList();
            var now = DateTime.UtcNow;
            foreach (var config in configs) config.CreatedAt = now;

            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await context.MachineCapacityConfigs.AddRangeAsync(configs);
                await context.SaveChangesAsync();
                logger.LogInformation("Seeded {Count} machine capacity configs.", configs.Count);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding machine capacity configs.");
        }
    }

    /// <summary>
    /// Seeds pricing configurations for all material + process combinations.
    /// </summary>
    public static async Task SeedPricingConfigurationsAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        try
        {
            var catalogConfigurations = PricingCatalogSeedData.GetPricingConfigurations().ToList();
            var catalogIds = catalogConfigurations.Select(config => config.Id).ToList();
            var existingCatalogConfigurations = await context.Configurations
                .Where(config => catalogIds.Contains(config.Id))
                .ToDictionaryAsync(config => config.Id);
            var now = DateTime.UtcNow;
            var addedCount = 0;
            var updatedCount = 0;

            foreach (var catalogConfiguration in catalogConfigurations)
            {
                if (existingCatalogConfigurations.TryGetValue(catalogConfiguration.Id, out var existingConfiguration))
                {
                    if (existingConfiguration.MaterialCode != catalogConfiguration.MaterialCode
                        || existingConfiguration.ManufacturingProcessCode != catalogConfiguration.ManufacturingProcessCode)
                    {
                        existingConfiguration.MaterialCode = catalogConfiguration.MaterialCode;
                        existingConfiguration.ManufacturingProcessCode = catalogConfiguration.ManufacturingProcessCode;
                        updatedCount++;
                    }

                    continue;
                }

                catalogConfiguration.CreatedAt = now;
                await context.Configurations.AddAsync(catalogConfiguration);
                addedCount++;
            }

            if (addedCount == 0 && updatedCount == 0)
            {
                logger.LogInformation("Pricing configurations already match the catalog. Skipping.");
                return;
            }

            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await context.SaveChangesAsync();
                logger.LogInformation(
                    "Reconciled pricing configurations: added {AddedCount}, stable codes updated {UpdatedCount}.",
                    addedCount,
                    updatedCount);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding pricing configurations.");
        }
    }
}
