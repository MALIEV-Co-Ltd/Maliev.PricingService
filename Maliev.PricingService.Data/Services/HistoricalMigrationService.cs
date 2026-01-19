using Maliev.PricingService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;

namespace Maliev.PricingService.Data.Services;

/// <summary>
/// Service to import legacy pricing data from a secondary PostgreSQL database.
/// </summary>
public class HistoricalMigrationService
{
    private readonly PricingDbContext _context;
    private readonly ILogger<HistoricalMigrationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoricalMigrationService"/> class.
    /// </summary>
    /// <param name="context">The pricing database context.</param>
    /// <param name="logger">The logger instance.</param>
    public HistoricalMigrationService(PricingDbContext context, ILogger<HistoricalMigrationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Migrates historical pricing data from a legacy PostgreSQL database.
    /// </summary>
    /// <param name="legacyConnectionString">The connection string to the legacy database.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task MigrateLegacyDataAsync(string legacyConnectionString, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting historical pricing data migration.");

        using var connection = new NpgsqlConnection(legacyConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = new NpgsqlCommand("SELECT material_id, process_id, material_price, support_price, machine_rate, print_speed, setup_cost, min_price, effective_from FROM legacy_pricing WHERE is_migrated = false", connection);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var configurations = new List<PricingConfiguration>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var config = new PricingConfiguration
            {
                MaterialId = reader.GetGuid(0),
                ManufacturingProcessId = reader.GetGuid(1),
                MaterialPricePerCm3 = reader.GetDecimal(2),
                SupportMaterialPricePerCm3 = reader.GetDecimal(3),
                MachineHourlyRate = reader.GetDecimal(4),
                PrintSpeedCm3PerHour = reader.GetDecimal(5),
                SetupCostFlat = reader.GetDecimal(6),
                MinimumOrderPrice = reader.GetDecimal(7),
                EffectiveFrom = reader.GetDateTime(8),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "MigrationService",
                IsActive = true
            };

            configurations.Add(config);
        }

        if (configurations.Any())
        {
            await _context.PricingConfigurations.AddRangeAsync(configurations, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully migrated {Count} records.", configurations.Count);
        }
        else
        {
            _logger.LogInformation("No legacy records found to migrate.");
        }
    }
}
