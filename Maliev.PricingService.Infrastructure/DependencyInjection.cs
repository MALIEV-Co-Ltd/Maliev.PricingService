using Maliev.PricingService.Infrastructure.Persistence;
using Maliev.PricingService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.PricingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<Persistence.PricingDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IPricingDbContext>(provider => provider.GetRequiredService<Persistence.PricingDbContext>());
        services.AddScoped<Services.HistoricalMigrationService>();

        return services;
    }
}
