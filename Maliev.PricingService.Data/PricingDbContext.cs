using Maliev.PricingService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PricingService.Data;

/// <summary>
/// Database context for the Pricing Service.
/// </summary>
public class PricingDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PricingDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to configure the context.</param>
    public PricingDbContext(DbContextOptions<PricingDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the pricing configurations.
    /// </summary>
    public DbSet<PricingConfiguration> PricingConfigurations => Set<PricingConfiguration>();

    /// <summary>
    /// Gets or sets the pricing audit records.
    /// </summary>
    public DbSet<PricingAuditRecord> PricingAuditRecords => Set<PricingAuditRecord>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PricingConfiguration Configuration
        modelBuilder.Entity<PricingConfiguration>(builder =>
        {
            builder.ToTable("pricing_configurations");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.MaterialPricePerCm3).HasPrecision(18, 6);
            builder.Property(x => x.SupportMaterialPricePerCm3).HasPrecision(18, 6);
            builder.Property(x => x.MachineHourlyRate).HasPrecision(18, 2);
            builder.Property(x => x.PrintSpeedCm3PerHour).HasPrecision(18, 4);
            builder.Property(x => x.RowVersion).IsRowVersion();
            builder.HasIndex(x => new { x.MaterialId, x.ManufacturingProcessId, x.EffectiveFrom }).IsUnique();
        });

        // PricingAuditRecord Configuration
        modelBuilder.Entity<PricingAuditRecord>(builder =>
        {
            builder.ToTable("pricing_audit_records");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.InputVolumeCm3).HasPrecision(18, 6);
            builder.Property(x => x.InputSupportVolumeCm3).HasPrecision(18, 6);
            builder.Property(x => x.InputSurfaceAreaCm2).HasPrecision(18, 6);
            builder.Property(x => x.TotalUnitPrice).HasPrecision(18, 2);
            builder.Property(x => x.TotalPrice).HasPrecision(18, 2);
        });
    }
}
