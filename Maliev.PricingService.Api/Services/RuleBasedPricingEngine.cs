namespace Maliev.PricingService.Api.Services;

using Maliev.PricingService.Data.Entities;
using System.Diagnostics;

/// <summary>
/// Rule-based implementation of the pricing engine.
/// </summary>
public class RuleBasedPricingEngine : IPricingEngine
{
    /// <inheritdoc/>
    public PricingStrategy Strategy => PricingStrategy.RuleBased;

    /// <inheritdoc/>
    public Task<PricingResult> CalculateAsync(PricingRequest request, PricingConfiguration config, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Material Cost
        decimal materialCost = request.Geometry.VolumeCm3 * config.MaterialPricePerCm3;
        decimal supportMaterialCost = request.Geometry.SupportVolumeCm3 * config.SupportMaterialPricePerCm3;
        decimal totalMaterialCost = materialCost + supportMaterialCost;

        // 2. Machine Cost (Time-based)
        decimal printTimeHours = config.PrintSpeedCm3PerHour > 0 ? request.Geometry.VolumeCm3 / config.PrintSpeedCm3PerHour : 0;
        decimal machineCost = printTimeHours * config.MachineHourlyRate;

        // 3. Setup Cost
        decimal setupCost = config.SetupCostFlat;

        // 4. Base Manufacturing Cost
        decimal baseCost = totalMaterialCost + machineCost + setupCost;

        // 5. Complexity Surcharge
        decimal complexitySurcharge = 0;
        if (request.Geometry.VolumeCm3 > 0)
        {
            decimal surfaceToVolumeRatio = request.Geometry.SurfaceAreaCm2 / request.Geometry.VolumeCm3;
            if (surfaceToVolumeRatio > config.ComplexityThreshold)
            {
                complexitySurcharge = baseCost * (config.ComplexitySurchargePercent / 100m);
            }
        }

        decimal subtotal = baseCost + complexitySurcharge;

        // 6. Apply Margin
        decimal marginAmount = subtotal * (config.MarginMultiplier - 1.0m);
        decimal unitPrice = subtotal + marginAmount;

        // 7. Minimum Price Check
        if (unitPrice < config.MinimumOrderPrice)
        {
            unitPrice = config.MinimumOrderPrice;
        }

        decimal totalPrice = unitPrice * request.Quantity;

        sw.Stop();

        return Task.FromResult(new PricingResult
        {
            Strategy = PricingStrategy.RuleBased,
            MaterialCost = Math.Round(materialCost, 2),
            SupportMaterialCost = Math.Round(supportMaterialCost, 2),
            MachineTimeCost = Math.Round(machineCost, 2),
            SetupCost = Math.Round(setupCost, 2),
            ComplexitySurcharge = Math.Round(complexitySurcharge, 2),
            SubtotalBeforeMargin = Math.Round(subtotal, 2),
            MarginAmount = Math.Round(marginAmount, 2),
            TotalUnitPrice = Math.Round(unitPrice, 2),
            TotalPrice = Math.Round(totalPrice, 2),
            ConfidenceLevel = 1.0m,
            CalculationDuration = sw.Elapsed
        });
    }
}
