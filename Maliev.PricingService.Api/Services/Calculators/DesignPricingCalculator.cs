namespace Maliev.PricingService.Api.Services.Calculators;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Calculator for 3D Design technology.
/// </summary>
public class DesignPricingCalculator : IPricingCalculator
{
    /// <inheritdoc/>
    public ManufacturingTechnology Technology => ManufacturingTechnology.Design;

    /// <inheritdoc/>
    public Task<PricingResult> CalculateAsync(
        PricingRequest request,
        MaterialData material,
        MachineRates rates,
        CancellationToken cancellationToken = default)
    {
        // TODO: Integrate with chatbot service to use LLM for design complexity estimation.
        // The LLM will analyze the design requirements and provide a more accurate cost estimate
        // based on complexity, scope, and expected effort. For now, returns a flat minimum fee.
        // Formula from spec:
        // return 500
        decimal totalUnitPrice = 500m;

        return Task.FromResult(new PricingResult
        {
            Strategy = PricingStrategy.RuleBased,
            MaterialCost = 0,
            SupportMaterialCost = 0,
            MachineTimeCost = 0,
            SetupCost = totalUnitPrice,
            ComplexitySurcharge = 0,
            SubtotalBeforeMargin = totalUnitPrice,
            MarginAmount = 0,
            TotalUnitPrice = totalUnitPrice,
            TotalPrice = totalUnitPrice * request.Quantity,
            ConfidenceLevel = 1.0m,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            CalculationDuration = TimeSpan.Zero,
            Notes = "Design price is a minimum estimate. Final price confirmed after scope discussion."
        });
    }
}
