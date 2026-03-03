using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maliev.PricingService.Application.Services;

public class RuleBasedPricingEngine : IPricingEngine
{
    private readonly ILogger<RuleBasedPricingEngine> _logger;
    private readonly Dictionary<string, IPricingCalculator> _calculators;

    public RuleBasedPricingEngine(ILogger<RuleBasedPricingEngine> logger)
    {
        _logger = logger;
        _calculators = new Dictionary<string, IPricingCalculator>(StringComparer.OrdinalIgnoreCase)
        {
            { "FDM", new FdmPricingCalculator() },
            { "Fused Deposition Modeling", new FdmPricingCalculator() },
            { "SLA", new SlaPricingCalculator() },
            { "Stereolithography", new SlaPricingCalculator() },
            { "DLP", new SlaPricingCalculator() },
            { "CNC", new CncPricingCalculator() },
            { "CNC Machining", new CncPricingCalculator() }
        };
    }

    public async Task<PricingResult> CalculateAsync(PricingRequest request, PricingConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating rule-based price for {MaterialCode}, Process: {ProcessName}", 
            request.MaterialCode, request.ManufacturingProcessName);

        var processName = request.ManufacturingProcessName;
        
        if (!_calculators.TryGetValue(processName, out var calculator))
        {
            calculator = _calculators["FDM"];
            _logger.LogWarning("Unknown process {ProcessName}, defaulting to FDM", processName);
        }

        var processParameters = new Dictionary<string, string>();
        
        var result = calculator.Calculate(
            request.Geometry.VolumeCm3,
            request.Geometry.SupportVolumeCm3,
            request.Geometry.SurfaceAreaCm2,
            request.Geometry.BoundingBoxX,
            request.Geometry.BoundingBoxY,
            request.Geometry.BoundingBoxZ,
            configuration.MaterialPricePerCm3,
            configuration.MachineHourlyRate,
            configuration.SetupCostFlat,
            configuration.MinimumOrderPrice,
            processParameters);

        return new PricingResult
        {
            UnitPrice = result,
            TotalAmount = result * request.Quantity,
            EngineName = $"Rule-v1-{calculator.TechnologyName}"
        };
    }
}
