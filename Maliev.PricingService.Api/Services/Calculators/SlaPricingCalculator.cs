namespace Maliev.PricingService.Api.Services.Calculators;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Calculator for SLA (Stereolithography) technology.
/// </summary>
public class SlaPricingCalculator : IPricingCalculator
{
    /// <inheritdoc/>
    public ManufacturingTechnology Technology => ManufacturingTechnology.Sla;

    /// <inheritdoc/>
    public Task<PricingResult> CalculateAsync(
        PricingRequest request,
        MaterialData material,
        MachineRates rates,
        CancellationToken cancellationToken = default)
    {
        // Formula from spec:
        // totalLayers = HeightMm / LayerHeightMm
        decimal totalLayers = request.HeightMm > 0 ? request.HeightMm / request.LayerHeightMm : 1;

        // printTimeHours = totalLayers * (material.SlaLayerExposure + material.SlaLiftTime) / 3600
        decimal printTimeHours = (totalLayers * (material.SlaLayerExposure + material.SlaLiftTime)) / 3600m;

        // weightGrams = volumeCm3 * material.Density (density is g/cm³, volume is cm³, result is grams)
        decimal weightGrams = request.Geometry.VolumeCm3 * material.Density;

        // materialCost = weightGrams * (material.CostPerKg / 1000)
        decimal materialCost = weightGrams * (material.CostPerKg / 1000m);

        // machineTimeCost = printTimeHours * rates.SlaMachineHourlyRate
        decimal machineTimeCost = printTimeHours * rates.SlaMachineHourlyRate;

        // total = materialCost + machineTimeCost + rates.SlaSetupFee
        decimal subtotal = materialCost + machineTimeCost + rates.SlaSetupFee;

        // total = Max(total, 500)
        decimal totalUnitPrice = Math.Max(subtotal, 500m);

        return Task.FromResult(new PricingResult
        {
            Strategy = PricingStrategy.RuleBased,
            MaterialCost = Math.Round(materialCost, 2),
            SupportMaterialCost = 0,
            MachineTimeCost = Math.Round(machineTimeCost, 2),
            SetupCost = Math.Round(rates.SlaSetupFee, 2),
            ComplexitySurcharge = 0,
            SubtotalBeforeMargin = Math.Round(subtotal, 2),
            MarginAmount = 0,
            TotalUnitPrice = Math.Round(totalUnitPrice, 2),
            TotalPrice = Math.Round(totalUnitPrice * request.Quantity, 2),
            ConfidenceLevel = 1.0m,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            CalculationDuration = TimeSpan.Zero
        });
    }
}
