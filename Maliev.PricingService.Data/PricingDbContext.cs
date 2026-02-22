using Maliev.PricingService.Data.Configurations;
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

    /// <summary>
    /// Gets or sets the pricing training data.
    /// </summary>
    public DbSet<PricingTrainingData> PricingTrainingData => Set<PricingTrainingData>();

    /// <summary>
    /// Gets or sets the pricing snapshots.
    /// </summary>
    public DbSet<PricingSnapshot> PricingSnapshots => Set<PricingSnapshot>();

    /// <summary>
    /// Gets or sets the pricing models.
    /// </summary>
    public DbSet<PricingModel> PricingModels => Set<PricingModel>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PricingSnapshot Configuration
        modelBuilder.ApplyConfiguration(new PricingSnapshotConfiguration());

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
            builder.Property(x => x.RowVersion).HasColumnName("row_version").HasDefaultValueSql("decode('0000000000000000', 'hex')");
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
            builder.Property(x => x.MaterialCost).HasPrecision(18, 2);
            builder.Property(x => x.SupportMaterialCost).HasPrecision(18, 2);
            builder.Property(x => x.MachineTimeCost).HasPrecision(18, 2);
            builder.Property(x => x.SetupCost).HasPrecision(18, 2);
            builder.Property(x => x.ComplexitySurcharge).HasPrecision(18, 2);
            builder.Property(x => x.SubtotalBeforeMargin).HasPrecision(18, 2);
            builder.Property(x => x.MarginAmount).HasPrecision(18, 2);
            builder.Property(x => x.TotalUnitPrice).HasPrecision(18, 2);
            builder.Property(x => x.TotalPrice).HasPrecision(18, 2);
            builder.Property(x => x.ConfidenceLevel).HasPrecision(18, 2);
        });

        // PricingTrainingData Configuration
        modelBuilder.Entity<PricingTrainingData>(builder =>
        {
            builder.ToTable("pricing_training_data");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.ActualMaterialUsedCm3).HasPrecision(18, 6);
            builder.Property(x => x.ActualPrintTimeHours).HasPrecision(18, 4);
            builder.Property(x => x.ActualLaborHours).HasPrecision(18, 4);
            builder.Property(x => x.ActualTotalCost).HasPrecision(18, 2);
            builder.Property(x => x.ActualProfitMargin).HasPrecision(18, 4);

            builder.HasOne(x => x.PricingAuditRecord)
                   .WithOne(x => x.TrainingData)
                   .HasForeignKey<PricingTrainingData>(x => x.PricingAuditRecordId);
        });

        // PricingModel Configuration
        modelBuilder.Entity<PricingModel>(builder =>
        {
            builder.ToTable("pricing_models");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.MeanAbsoluteError).HasPrecision(18, 6);
            builder.Property(x => x.MeanAbsolutePercentageError).HasPrecision(18, 4);
            builder.Property(x => x.RSquared).HasPrecision(18, 4);
            builder.Property(x => x.RowVersion).HasColumnName("row_version").HasDefaultValueSql("decode('0000000000000000', 'hex')");
        });
    }
}
