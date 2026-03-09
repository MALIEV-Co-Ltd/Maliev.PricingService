using Maliev.PricingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maliev.PricingService.Application;

/// <summary>Design-time factory.</summary>
public class PricingDbContextFactory : IDesignTimeDbContextFactory<PricingDbContext>
{
    /// <summary>Creates the context.</summary>
    public PricingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PricingDbContext>();
        // Use a dummy connection string for design-time operations
        optionsBuilder.UseNpgsql("Host=localhost;Database=PricingDb;Username=postgres;Password=password");

        return new PricingDbContext(optionsBuilder.Options);
    }
}
