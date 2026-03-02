using Maliev.PricingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PricingService.Application.Interfaces;

public interface IPricingDbContext
{
    DbSet<PricingSnapshot> Snapshots { get; }
    DbSet<PricingAuditRecord> AuditRecords { get; }
    DbSet<PricingConfiguration> Configurations { get; }
    DbSet<PricingModel> Models { get; }
    DbSet<PricingTrainingData> TrainingData { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
