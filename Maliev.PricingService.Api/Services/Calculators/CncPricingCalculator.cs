namespace Maliev.PricingService.Api.Services.Calculators;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Calculator for CNC Machining technology.
/// </summary>
public class CncPricingCalculator : IPricingCalculator
{
    /// <inheritdoc/>
    public ManufacturingTechnology Technology => ManufacturingTechnology.Cnc;

    /// <inheritdoc/>
    public Task<PricingResult> CalculateAsync(
        PricingRequest request,
        MaterialData material,
        MachineRates rates,
        CancellationToken cancellationToken = default)
    {
        // Formula from spec:
        // All dimensions from bounding box — in mm
        // blockVolumeMm3 = BoundingBoxX * BoundingBoxY * BoundingBoxZ
        decimal blockVolumeMm3 = request.Geometry.BoundingBoxX * request.Geometry.BoundingBoxY * request.Geometry.BoundingBoxZ;

        // partVolumeMm3 = request.Geometry.VolumeCm3 * 1000
        decimal partVolumeMm3 = request.Geometry.VolumeCm3 * 1000m;

        // removalVolumeMm3 = Max(blockVolumeMm3 - partVolumeMm3, 0)
        decimal removalVolumeMm3 = Math.Max(blockVolumeMm3 - partVolumeMm3, 0);

        // blockWeightGrams = blockVolumeMm3 * material.Density / 1000
        decimal blockWeightGrams = blockVolumeMm3 * material.Density / 1000m;

        // blockCost = blockWeightGrams * (material.CostPerKg / 1000)
        decimal blockCost = blockWeightGrams * (material.CostPerKg / 1000m);

        // effectiveMrr = rates.CncMaterialRemovalRate * material.CncMachinabilityRating
        decimal effectiveMrr = rates.CncMaterialRemovalRate * material.CncMachinabilityRating;

        // machiningTimeHours = removalVolumeMm3 / effectiveMrr
        decimal machiningTimeHours = effectiveMrr > 0 ? removalVolumeMm3 / effectiveMrr : 0;

        // complexityFactor = request.Geometry.SurfaceAreaCm2 / request.Geometry.VolumeCm3
        decimal complexityFactor = request.Geometry.VolumeCm3 > 0 
            ? request.Geometry.SurfaceAreaCm2 / request.Geometry.VolumeCm3 
            : 1;

        // machineTimeCost = machiningTimeHours * rates.CncMachineHourlyRate * complexityFactor
        decimal machineTimeCost = machiningTimeHours * rates.CncMachineHourlyRate * complexityFactor;

        // total = blockCost + machineTimeCost + rates.CncSetupFee
        decimal subtotal = blockCost + machineTimeCost + rates.CncSetupFee;

        // total = Max(total, 2500)
        decimal totalUnitPrice = Math.Max(subtotal, 2500m);

        return Task.FromResult(new PricingResult
        {
            Strategy = PricingStrategy.RuleBased,
            MaterialCost = Math.Round(blockCost, 2),
            SupportMaterialCost = 0,
            MachineTimeCost = Math.Round(machineTimeCost, 2),
            SetupCost = Math.Round(rates.CncSetupFee, 2),
            ComplexitySurcharge = 0, // Complexity is factored into machineTimeCost
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
