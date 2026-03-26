using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Infrastructure.Clients;
using Maliev.PricingService.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.PricingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPricingDbContext>(provider => provider.GetRequiredService<Persistence.PricingDbContext>());
        services.AddScoped<Services.HistoricalMigrationService>();

        services.AddHttpClient<IJobServiceClient, JobServiceClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:JobService:BaseUrl"] ?? "http://localhost:5004");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }
}
