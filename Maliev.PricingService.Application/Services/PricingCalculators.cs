namespace Maliev.PricingService.Application.Services;

public interface IPricingCalculator
{
    string TechnologyName { get; }
    decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2, 
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        Dictionary<string, string> processParameters);
}

public class FdmPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "FDM";

    public decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        Dictionary<string, string> processParameters)
    {
        decimal density = 1.04m;
        bool hasSupport = supportVolumeCm3 > 0;
        
        if (processParameters.TryGetValue("Density", out var densityStr) && decimal.TryParse(densityStr, out var parsedDensity))
        {
            density = parsedDensity;
        }

        decimal volumeWithSupport = hasSupport ? volumeCm3 + (supportVolumeCm3 * 1.2m) : volumeCm3;
        decimal materialWeightGrams = volumeWithSupport * density * 1000;
        decimal materialCost = (materialWeightGrams / 1000) * materialCostPerCm3;

        decimal totalLayers = boundingBoxZ > 0 ? 100 / 0.2m : 100;
        if (processParameters.TryGetValue("LayerHeight", out var layerHeightStr) && decimal.TryParse(layerHeightStr, out var layerHeight) && layerHeight > 0)
        {
            totalLayers = boundingBoxZ / layerHeight;
        }

        decimal volumetricFlowRate = 15m;
        if (processParameters.TryGetValue("FlowRate", out var flowRateStr) && decimal.TryParse(flowRateStr, out var parsedFlowRate))
        {
            volumetricFlowRate = parsedFlowRate;
        }

        decimal calcLayerTime = volumeCm3 * 1000 / volumetricFlowRate / (decimal)totalLayers;
        
        decimal minLayerTime = 5m;
        if (processParameters.TryGetValue("MinLayerTime", out var minLayerTimeStr) && decimal.TryParse(minLayerTimeStr, out var parsedMinLayerTime))
        {
            minLayerTime = parsedMinLayerTime;
        }

        decimal effectiveLayerTime = Math.Max(calcLayerTime, minLayerTime);
        decimal printTimeHours = effectiveLayerTime * (decimal)totalLayers / 3600;

        decimal machineCost = printTimeHours * machineHourlyRate;
        decimal total = materialCost + machineCost + setupFee;

        return Math.Max(total, minimumOrderPrice);
    }
}

public class SlaPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "SLA";

    public decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        Dictionary<string, string> processParameters)
    {
        decimal totalLayers = boundingBoxZ > 0 ? 100 / 0.05m : 2000;
        if (processParameters.TryGetValue("LayerHeight", out var layerHeightStr) && decimal.TryParse(layerHeightStr, out var layerHeight) && layerHeight > 0)
        {
            totalLayers = boundingBoxZ / layerHeight;
        }

        decimal layerExposure = 2.5m;
        if (processParameters.TryGetValue("LayerExposure", out var exposureStr) && decimal.TryParse(exposureStr, out var parsedExposure))
        {
            layerExposure = parsedExposure;
        }

        decimal liftTime = 3m;
        if (processParameters.TryGetValue("LiftTime", out var liftTimeStr) && decimal.TryParse(liftTimeStr, out var parsedLiftTime))
        {
            liftTime = parsedLiftTime;
        }

        decimal printTimeHours = totalLayers * (layerExposure + liftTime) / 3600;

        decimal materialCost = volumeCm3 * materialCostPerCm3;
        if (processParameters.TryGetValue("SupportMaterialPricePerCm3", out var supportCostStr) && decimal.TryParse(supportCostStr, out var supportCost))
        {
            materialCost += supportVolumeCm3 * supportCost;
        }

        decimal machineCost = printTimeHours * machineHourlyRate;
        decimal total = materialCost + machineCost + setupFee;

        return Math.Max(total, minimumOrderPrice);
    }
}

public class CncPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "CNC";

    public decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        Dictionary<string, string> processParameters)
    {
        decimal blockVolume = boundingBoxX * boundingBoxY * boundingBoxZ;
        decimal removalVolume = Math.Max(0, blockVolume - volumeCm3);

        decimal mrr = 50m;
        if (processParameters.TryGetValue("MRR", out var mrrStr) && decimal.TryParse(mrrStr, out var parsedMrr))
        {
            mrr = parsedMrr;
        }

        decimal machinabilityRating = 1.0m;
        if (processParameters.TryGetValue("MachinabilityRating", out var machRatingStr) && decimal.TryParse(machRatingStr, out var parsedRating))
        {
            machinabilityRating = parsedRating;
        }

        decimal machiningTimeHours = removalVolume / (mrr * machinabilityRating);

        decimal complexityFactor = 1.0m;
        if (volumeCm3 > 0)
        {
            decimal surfaceAreaToVolume = surfaceAreaCm2 / (volumeCm3 * 10000);
            if (surfaceAreaToVolume > 2.0m)
            {
                complexityFactor = 1.15m;
            }
        }

        decimal blockMaterialCost = blockVolume * materialCostPerCm3;
        decimal machineCost = machiningTimeHours * machineHourlyRate * complexityFactor;
        decimal total = blockMaterialCost + machineCost + setupFee;

        return Math.Max(total, minimumOrderPrice);
    }
}
