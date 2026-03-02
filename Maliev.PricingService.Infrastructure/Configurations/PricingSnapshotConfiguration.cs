using Maliev.PricingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maliev.PricingService.Application.Configurations;

/// <summary>
/// EF Core configuration for PricingSnapshot.
/// </summary>
public class PricingSnapshotConfiguration : IEntityTypeConfiguration<PricingSnapshot>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<PricingSnapshot> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.OrderId);
        builder.HasIndex(s => s.EmployeeId);

        builder.Property(s => s.OrderId).IsRequired().HasMaxLength(50);
        builder.Property(s => s.EmployeeId).IsRequired();
        builder.Property(s => s.Technology).HasMaxLength(50);
        builder.Property(s => s.Status).HasMaxLength(20);

        builder.Property(s => s.CalculatedPrice).HasPrecision(18, 2);
        builder.Property(s => s.ManualOverridePrice).HasPrecision(18, 2);

        builder.HasOne(s => s.PricingAuditRecord)
            .WithMany()
            .HasForeignKey(s => s.PricingAuditRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.SupersededBy)
            .WithOne()
            .HasForeignKey<PricingSnapshot>(s => s.SupersededById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
