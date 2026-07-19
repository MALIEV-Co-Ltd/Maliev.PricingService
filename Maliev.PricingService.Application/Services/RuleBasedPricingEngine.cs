using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maliev.PricingService.Application.Services;

public class RuleBasedPricingEngine : IPricingEngine
{
    private readonly ILogger<RuleBasedPricingEngine> _logger;
    private readonly IPricingCalculatorRegistry _registry;

    public RuleBasedPricingEngine(
        ILogger<RuleBasedPricingEngine> logger,
        IPricingCalculatorRegistry registry)
    {
        _logger = logger;
        _registry = registry;
    }

    public Task<EngineResult> CalculateAsync(
        PricingRequest request,
        PricingConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Calculating rule-based price for {MaterialCode}, Process: {ProcessName}",
            request.MaterialCode, request.ManufacturingProcessName);

        if (!_registry.TryResolve(request.ManufacturingProcessName, out var calculator))
        {
            _logger.LogError(
                "No calculator registered for process '{ProcessName}'. Returning zero price.",
                request.ManufacturingProcessName);

            return Task.FromResult(new EngineResult(
                new CostBreakdown(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m),
                "UnknownProcess"));
        }

        var processParameters = BuildProcessParameters(configuration, request);
        var ctx = new PricingContext(
            Geometry: request.Geometry,
            MaterialPricePerCm3: configuration.MaterialPricePerCm3,
            MachineHourlyRate: configuration.MachineHourlyRate,
            SetupFee: configuration.SetupCostFlat,
            MinimumOrderPrice: configuration.MinimumOrderPrice,
            Dfm: request.Dfm,
            ProcessParameters: processParameters);

        var breakdown = calculator.Calculate(ctx);

        return Task.FromResult(new EngineResult(
            breakdown,
            $"Rule-v1-{calculator.TechnologyName}"));
    }

    private static IReadOnlyDictionary<string, string> BuildProcessParameters(
        PricingConfiguration config, PricingRequest request)
    {
        var dict = new Dictionary<string, string>();

        // ── Config-sourced parameters ──────────────────────────────────────────
        if (config.PrintSpeedCm3PerHour > 0)
            dict["FlowRate"] = config.PrintSpeedCm3PerHour.ToString("G");

        if (config.DensityGramPerCm3 is > 0m)
            dict["Density"] = config.DensityGramPerCm3.Value.ToString("G");

        if (config.ComplexityThreshold > 0)
            dict["ComplexityThreshold"] = config.ComplexityThreshold.ToString("G");
        if (config.ComplexitySurchargePercent > 0)
            dict["ComplexitySurchargePercent"] = config.ComplexitySurchargePercent.ToString("G");

        if (config.SupportMaterialPricePerCm3 > 0)
            dict["SupportMaterialPricePerCm3"] = config.SupportMaterialPricePerCm3.ToString("G");

        // ── Request-sourced parameters (process-specific nullable extensions) ──
        if (request.WeldLengthMm.HasValue) dict["WeldLengthMm"] = request.WeldLengthMm.Value.ToString();
        if (request.CutLengthMm.HasValue) dict["CutLengthMm"] = request.CutLengthMm.Value.ToString();
        if (request.BendCount.HasValue) dict["BendCount"] = request.BendCount.Value.ToString();
        if (request.ElectrodeCount.HasValue) dict["ElectrodeCount"] = request.ElectrodeCount.Value.ToString();
        if (request.PointCountThousands.HasValue) dict["PointCountThousands"] = request.PointCountThousands.Value.ToString();
        if (request.LayerCount.HasValue) dict["LayerCount"] = request.LayerCount.Value.ToString();
        if (request.WeightKg.HasValue) dict["WeightKg"] = request.WeightKg.Value.ToString("G");
        if (request.ThicknessMm.HasValue) dict["ThicknessMm"] = request.ThicknessMm.Value.ToString("G");
        if (request.VendorQuoteAmount.HasValue) dict["VendorQuoteAmount"] = request.VendorQuoteAmount.Value.ToString("G");
        if (request.VendorQuoteCurrency != null) dict["VendorQuoteCurrency"] = request.VendorQuoteCurrency;
        if (request.VendorQuoteMarkupOverride.HasValue) dict["VendorQuoteMarkupOverride"] = request.VendorQuoteMarkupOverride.Value.ToString("G");

        return dict;
    }
}
