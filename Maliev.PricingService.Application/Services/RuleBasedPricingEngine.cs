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
            // FDM
            { "FDM", new FdmPricingCalculator() },
            { "Fused Deposition Modeling", new FdmPricingCalculator() },
            { "3D Printing (FDM)", new FdmPricingCalculator() },
            // SLA/DLP
            { "SLA", new SlaPricingCalculator() },
            { "Stereolithography", new SlaPricingCalculator() },
            { "DLP", new SlaPricingCalculator() },
            { "3D Printing (SLA)", new SlaPricingCalculator() },
            { "3D Printing (SLA/DLP)", new SlaPricingCalculator() },
            // CNC (legacy)
            { "CNC", new CncPricingCalculator() },
            { "CNC Machining", new CncPricingCalculator() },
            // CNC Milling
            { "CNC_MILL", new CncMillPricingCalculator() },
            { "CNC Milling", new CncMillPricingCalculator() },
            // CNC Turning
            { "CNC_TURN", new CncTurnPricingCalculator() },
            { "CNC Turning", new CncTurnPricingCalculator() },
            // SLS
            { "SLS", new SlsPricingCalculator() },
            { "3D Printing (SLS)", new SlsPricingCalculator() },
            // MJF
            { "MJF", new MjfPricingCalculator() },
            { "3D Printing (MJF)", new MjfPricingCalculator() },
            // Material Jetting
            { "MJ", new MjPricingCalculator() },
            { "3D Printing (Material Jetting)", new MjPricingCalculator() },
            // Binder Jetting
            { "BJ", new BjPricingCalculator() },
            { "3D Printing (Binder Jetting)", new BjPricingCalculator() },
            // DMLS
            { "DMLS", new DmlsPricingCalculator() },
            { "3D Printing (DMLS)", new DmlsPricingCalculator() },
            // Sheet Metal
            { "Sheet Metal", new CncPricingCalculator() },
            { "Sheet Metal Fabrication", new CncPricingCalculator() },
            // Injection Molding
            { "Injection Molding", new CncPricingCalculator() },
            // Scanning
            { "3D Scanning", new ScanningPricingCalculator() },
            { "3D Scanning (Raw STL)", new ScanningPricingCalculator() },
            { "3D Scanning + Reverse Engineering", new ScanningPricingCalculator() },
            { "Scanning", new ScanningPricingCalculator() },
            // Design
            { "3D Design", new DesignPricingCalculator() },
            { "Design", new DesignPricingCalculator() },
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
            configuration.MarginMultiplier,
            request.Dfm,
            processParameters);

        return new PricingResult
        {
            UnitPrice = result,
            TotalAmount = result * request.Quantity,
            EngineName = $"Rule-v1-{calculator.TechnologyName}"
        };
    }
}
