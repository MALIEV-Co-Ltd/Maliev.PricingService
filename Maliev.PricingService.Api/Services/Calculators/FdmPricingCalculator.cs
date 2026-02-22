namespace Maliev.PricingService.Api.Services.Calculators;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Calculator for FDM (Fused Deposition Modeling) technology.
/// </summary>
public class FdmPricingCalculator : IPricingCalculator
{
    /// <inheritdoc/>
    public ManufacturingTechnology Technology => ManufacturingTechnology.Fdm;

    /// <inheritdoc/>
    public Task<PricingResult> CalculateAsync(
        PricingRequest request,
        MaterialData material,
        MachineRates rates,
        CancellationToken cancellationToken = default)
    {
        // Formula from spec:
        // volumeMm3 = request.Geometry.VolumeCm3 * 1000
        // if SupportEnabled: volumeMm3 *= 1.2
        decimal volumeMm3 = request.Geometry.VolumeCm3 * 1000m;
        if (request.SupportEnabled)
        {
            volumeMm3 *= 1.2m;
        }

        // weightGrams = volumeMm3 * material.Density / 1000
        decimal weightGrams = volumeMm3 * material.Density / 1000m;

        // materialCost = weightGrams * (material.CostPerKg / 1000)
        decimal materialCost = weightGrams * (material.CostPerKg / 1000m);

        // totalLayers = HeightMm / LayerHeightMm
        decimal totalLayers = request.HeightMm > 0 ? request.HeightMm / request.LayerHeightMm : 1;

        // calcLayerTimeSec = volumeMm3 / material.FdmVolumetricFlowRate / totalLayers
        decimal calcLayerTimeSec = (material.FdmVolumetricFlowRate > 0 && totalLayers > 0) 
            ? volumeMm3 / material.FdmVolumetricFlowRate / totalLayers 
            : 0;

        // effectiveLayerTimeSec = Max(calcLayerTimeSec, material.FdmMinLayerTime)
        decimal effectiveLayerTimeSec = Math.Max(calcLayerTimeSec, material.FdmMinLayerTime);

        // printTimeHours = (effectiveLayerTimeSec * totalLayers) / 3600
        decimal printTimeHours = (effectiveLayerTimeSec * totalLayers) / 3600m;

        // machineTimeCost = printTimeHours * rates.FdmMachineHourlyRate
        decimal machineTimeCost = printTimeHours * rates.FdmMachineHourlyRate;

        // total = materialCost + machineTimeCost + rates.FdmSetupFee
        decimal subtotal = materialCost + machineTimeCost + rates.FdmSetupFee;

        // total = Max(total, 300)
        decimal totalUnitPrice = Math.Max(subtotal, 300m);

        return Task.FromResult(new PricingResult
        {
            Strategy = PricingStrategy.RuleBased,
            MaterialCost = Math.Round(materialCost, 2),
            SupportMaterialCost = 0, // Simplified for now, or could split if needed
            MachineTimeCost = Math.Round(machineTimeCost, 2),
            SetupCost = Math.Round(rates.FdmSetupFee, 2),
            ComplexitySurcharge = 0,
            SubtotalBeforeMargin = Math.Round(subtotal, 2),
            MarginAmount = 0, // Formulas in spec don't explicitly mention margin, but it's applied in Orchestrator
            TotalUnitPrice = Math.Round(totalUnitPrice, 2),
            TotalPrice = Math.Round(totalUnitPrice * request.Quantity, 2),
            ConfidenceLevel = 1.0m,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            CalculationDuration = TimeSpan.Zero // Set by engine
        });
    }
}
