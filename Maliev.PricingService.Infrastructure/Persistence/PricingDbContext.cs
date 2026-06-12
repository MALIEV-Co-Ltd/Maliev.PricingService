using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PricingService.Infrastructure.Persistence;

/// <summary>
/// Database context for the Pricing Service.
/// </summary>
public class PricingDbContext : DbContext, IPricingDbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PricingDbContext"/> class.
    /// </summary>
    /// <param name="options">The options to configure the context.</param>
    public PricingDbContext(DbContextOptions<PricingDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc/>
    public DbSet<PricingConfiguration> Configurations => Set<PricingConfiguration>();

    /// <inheritdoc/>
    public DbSet<PricingAuditRecord> AuditRecords => Set<PricingAuditRecord>();

    /// <inheritdoc/>
    public DbSet<PricingSnapshot> Snapshots => Set<PricingSnapshot>();

    /// <inheritdoc/>
    public DbSet<LeadTimeOption> LeadTimeOptions => Set<LeadTimeOption>();

    /// <inheritdoc/>
    public DbSet<VolumeDiscountTier> VolumeDiscountTiers => Set<VolumeDiscountTier>();

    /// <inheritdoc/>
    public DbSet<MachineCapacityConfig> MachineCapacityConfigs => Set<MachineCapacityConfig>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        // PricingSnapshot Configuration
        modelBuilder.Entity<PricingSnapshot>(builder =>
        {
            builder.ToTable("pricing_snapshots");
            builder.HasKey(x => x.Id);
            builder.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        // PricingConfiguration Configuration
        modelBuilder.Entity<PricingConfiguration>(builder =>
        {
            builder.ToTable("pricing_configurations");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.MaterialPricePerCm3).HasPrecision(18, 6);
            builder.Property(x => x.SupportMaterialPricePerCm3).HasPrecision(18, 6);
            builder.Property(x => x.MachineHourlyRate).HasPrecision(18, 2);
            builder.Property(x => x.PrintSpeedCm3PerHour).HasPrecision(18, 4);
            builder.HasIndex(x => new { x.MaterialId, x.ManufacturingProcessId, x.EffectiveFrom }).IsUnique();
            builder.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        // PricingAuditRecord Configuration
        modelBuilder.Entity<PricingAuditRecord>(builder =>
        {
            builder.ToTable("pricing_audit_records");
            builder.HasKey(x => x.Id);
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
            builder.Property(x => x.ConfigMaterialPricePerCm3).HasPrecision(18, 6);
            builder.Property(x => x.ConfigSupportPricePerCm3).HasPrecision(18, 6);
            builder.Property(x => x.ConfigMachineHourlyRate).HasPrecision(18, 2);
            builder.Property(x => x.ConfigMarginMultiplier).HasPrecision(18, 2);
            builder.Property(x => x.ConfidenceLevel).HasPrecision(18, 2);
        });

        // LeadTimeOption Configuration
        modelBuilder.Entity<LeadTimeOption>(builder =>
        {
            builder.ToTable("lead_time_options");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Code).IsRequired().HasMaxLength(50);
            builder.HasIndex(x => x.Code).IsUnique();
            builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
            builder.Property(x => x.PriceMultiplier).HasPrecision(6, 4).IsRequired();
            builder.Property(x => x.SortOrder).HasDefaultValue(0);
            builder.HasIndex(x => x.IsActive);
        });

        // VolumeDiscountTier Configuration
        modelBuilder.Entity<VolumeDiscountTier>(builder =>
        {
            builder.ToTable("volume_discount_tiers");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.DiscountPercent).HasPrecision(6, 2).IsRequired();
            builder.Property(x => x.SortOrder).HasDefaultValue(0);
            builder.HasIndex(x => x.MinQuantity);
            builder.HasIndex(x => x.IsActive);
        });

        // MachineCapacityConfig Configuration
        modelBuilder.Entity<MachineCapacityConfig>(builder =>
        {
            builder.ToTable("machine_capacity_configs");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.ProcessType).IsRequired().HasMaxLength(50);
            builder.HasIndex(x => x.ProcessType).IsUnique();
            builder.Property(x => x.AvgThroughputPartsPerDay).HasPrecision(18, 4).IsRequired();
            builder.HasIndex(x => x.IsActive);
            builder.Property<uint>("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }
}
