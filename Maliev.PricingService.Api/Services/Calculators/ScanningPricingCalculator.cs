namespace Maliev.PricingService.Api.Services.Calculators;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Calculator for 3D Scanning technology.
/// </summary>
public class ScanningPricingCalculator : IPricingCalculator
{
    /// <inheritdoc/>
    public ManufacturingTechnology Technology => ManufacturingTechnology.Scanning;

    /// <inheritdoc/>
    public Task<PricingResult> CalculateAsync(
        PricingRequest request,
        MaterialData material,
        MachineRates rates,
        CancellationToken cancellationToken = default)
    {
        // Formula from spec:
        // if request.ScanningTier == "ReverseEngineering": return 4500
        // else: return 2500
        // Handle null and case-insensitive comparison
        bool isReverseEngineering = string.Equals(
            request.ScanningTier,
            "ReverseEngineering",
            StringComparison.OrdinalIgnoreCase);
        decimal totalUnitPrice = isReverseEngineering ? 4500m : 2500m;

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
            Notes = "Scanning price is a minimum estimate. Final price confirmed by Maliev after inspection."
        });
    }
}
