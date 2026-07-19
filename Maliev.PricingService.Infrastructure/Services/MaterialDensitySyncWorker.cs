using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.PricingService.Infrastructure.Services;

/// <summary>
/// Background worker that refreshes DensityGramPerCm3 on PricingConfiguration
/// from MaterialService every 6 hours, so per-process density defaults stay accurate
/// without requiring a restart.
/// </summary>
public class MaterialDensitySyncWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MaterialDensitySyncWorker> _logger;
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(6);

    public MaterialDensitySyncWorker(IServiceProvider services, ILogger<MaterialDensitySyncWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay startup briefly so the host is fully ready before first sync
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncDensitiesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "MaterialDensitySyncWorker encountered an error; will retry at next interval");
            }

            await Task.Delay(SyncInterval, stoppingToken);
        }
    }

    private async Task SyncDensitiesAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        var materialClient = scope.ServiceProvider.GetRequiredService<IMaterialServiceClient>();

        var configs = await db.Configurations
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken);

        if (configs.Count == 0)
        {
            _logger.LogDebug("MaterialDensitySyncWorker: no active configurations to sync");
            return;
        }

        var distinctMaterialIds = configs.Select(c => c.MaterialId).ToHashSet();
        _logger.LogInformation("MaterialDensitySyncWorker: syncing densities for {Count} materials", distinctMaterialIds.Count);

        var materials = await materialClient.GetMaterialsAsync(cancellationToken);
        var densityMap = materials
            .Where(material => distinctMaterialIds.Contains(material.Id)
                && material.DensityGramPerCm3 > 0m)
            .ToDictionary(material => material.Id, material => material.DensityGramPerCm3);

        var missingCount = distinctMaterialIds.Count - densityMap.Count;
        if (missingCount > 0)
        {
            _logger.LogWarning(
                "MaterialDensitySyncWorker: material catalog did not include usable density for {MissingCount} configured materials",
                missingCount);
        }

        int updated = 0;
        foreach (var config in configs)
        {
            if (densityMap.TryGetValue(config.MaterialId, out var density)
                && config.DensityGramPerCm3 != density)
            {
                config.DensityGramPerCm3 = density;
                config.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("MaterialDensitySyncWorker: updated density on {Count} configurations", updated);
        }
        else
        {
            _logger.LogDebug("MaterialDensitySyncWorker: all densities already up to date");
        }
    }
}
