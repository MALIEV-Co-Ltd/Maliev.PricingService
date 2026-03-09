using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.PricingService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPricingEngine, RuleBasedPricingEngine>();
        services.AddScoped<IMLPricingEngine, MLPricingEngine>();
        services.AddScoped<IPricingOrchestrator, PricingOrchestrator>();

        return services;
    }
}
