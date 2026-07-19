using System.Diagnostics.CodeAnalysis;

namespace Maliev.PricingService.Application.Services;

public interface IPricingCalculatorRegistry
{
    bool TryResolve(string processName, [NotNullWhen(true)] out IPricingCalculator? calculator);
}

public sealed class PricingCalculatorRegistry : IPricingCalculatorRegistry
{
    private readonly IReadOnlyDictionary<string, IPricingCalculator> _map;

    public PricingCalculatorRegistry(IEnumerable<IPricingCalculator> calculators)
    {
        var map = new Dictionary<string, IPricingCalculator>(StringComparer.OrdinalIgnoreCase);
        foreach (var calc in calculators)
            foreach (var alias in calc.Aliases)
                map[alias] = calc;
        _map = map;
    }

    public bool TryResolve(string processName, [NotNullWhen(true)] out IPricingCalculator? calculator)
        => _map.TryGetValue(processName, out calculator);
}
