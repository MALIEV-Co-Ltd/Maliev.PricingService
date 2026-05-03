using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.PricingService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IPricingCalculator, FdmPricingCalculator>();
        services.AddSingleton<IPricingCalculator, SlaPricingCalculator>();
        services.AddSingleton<IPricingCalculator, CncPricingCalculator>();
        services.AddSingleton<IPricingCalculator, CncMillPricingCalculator>();
        services.AddSingleton<IPricingCalculator, CncTurnPricingCalculator>();
        services.AddSingleton<IPricingCalculator, SlsPricingCalculator>();
        services.AddSingleton<IPricingCalculator, MjfPricingCalculator>();
        services.AddSingleton<IPricingCalculator, MjPricingCalculator>();
        services.AddSingleton<IPricingCalculator, BjPricingCalculator>();
        services.AddSingleton<IPricingCalculator, DmlsPricingCalculator>();
        services.AddSingleton<IPricingCalculator, ScanningPricingCalculator>();
        services.AddSingleton<IPricingCalculator, DesignPricingCalculator>();
        services.AddSingleton<IPricingCalculator, SheetMetalPricingCalculator>();
        services.AddSingleton<IPricingCalculator, InjectionMouldingPricingCalculator>();
        services.AddSingleton<IPricingCalculator, SiliconeCastingPricingCalculator>();
        services.AddSingleton<IPricingCalculator, ManualInjectionMouldingPricingCalculator>();
        services.AddSingleton<IPricingCalculator, PaintingPricingCalculator>();
        services.AddSingleton<IPricingCalculator, SurfaceFinishingPricingCalculator>();
        services.AddSingleton<IPricingCalculator, EdmPricingCalculator>();
        services.AddSingleton<IPricingCalculator, GrindingPricingCalculator>();
        services.AddSingleton<IPricingCalculator, WeldingPricingCalculator>();
        services.AddSingleton<IPricingCalculator, StdMetalFabPricingCalculator>();
        services.AddSingleton<IPricingCalculator, DeviationAnalysisPricingCalculator>();
        services.AddSingleton<IPricingCalculator, ChinaOutsourcingPricingCalculator>();

        services.AddSingleton<IPricingCalculatorRegistry, PricingCalculatorRegistry>();
        services.AddScoped<IVolumeDiscountResolver, VolumeDiscountResolver>();
        services.AddScoped<IPricingEngine, RuleBasedPricingEngine>();
        services.AddScoped<IPricingOrchestrator, PricingOrchestrator>();

        return services;
    }
}
