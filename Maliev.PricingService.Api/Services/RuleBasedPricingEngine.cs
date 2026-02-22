namespace Maliev.PricingService.Api.Services;

using Maliev.PricingService.Api.Services.Calculators;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Rule-based implementation of the pricing engine.
/// Orchestrates technology-specific calculators.
/// </summary>
public class RuleBasedPricingEngine : IPricingEngine
{
    private readonly IEnumerable<IPricingCalculator> _calculators;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuleBasedPricingEngine"/> class.
    /// </summary>
    /// <param name="calculators">The technology-specific calculators.</param>
    public RuleBasedPricingEngine(IEnumerable<IPricingCalculator> calculators)
    {
        _calculators = calculators;
    }

    /// <inheritdoc/>
    public PricingStrategy Strategy => PricingStrategy.RuleBased;

    /// <inheritdoc/>
    public async Task<PricingResult> CalculateAsync(
        PricingRequest request,
        MaterialData material,
        MachineRates rates,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        var calculator = _calculators.FirstOrDefault(c => c.Technology == request.Technology)
            ?? throw new InvalidOperationException($"No pricing calculator registered for technology: {request.Technology}");

        var result = await calculator.CalculateAsync(request, material, rates, cancellationToken);

        sw.Stop();

        return result with
        {
            CalculationDuration = sw.Elapsed
        };
    }
}
