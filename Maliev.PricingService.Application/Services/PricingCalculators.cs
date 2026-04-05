using Maliev.PricingService.Application.DTOs;

namespace Maliev.PricingService.Application.Services;

public interface IPricingCalculator
{
    string TechnologyName { get; }
    decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2, 
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        decimal marginMultiplier, DfmMetrics? dfm,
        Dictionary<string, string> processParameters);
}

public class FdmPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "FDM";

    public decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        decimal marginMultiplier, DfmMetrics? dfm,
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

        decimal totalLayers = boundingBoxZ > 0 ? boundingBoxZ / 0.2m : 100;
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
        decimal baseTotal = materialCost + machineCost + setupFee;

        decimal dfmSurchargePercent = 0m;
        if (dfm != null)
        {
            if (dfm.ThinWallCount > 0)
            {
                dfmSurchargePercent += Math.Min(dfm.ThinWallCount * 5m, 15m);
            }
            if (dfm.SupportRequired && dfm.EstimatedSupportVolumeCm3.HasValue && dfm.EstimatedSupportVolumeCm3.Value > volumeCm3 * 0.5m)
            {
                dfmSurchargePercent = Math.Max(dfmSurchargePercent, 15m);
            }
        }

        decimal totalWithDfm = baseTotal * (1 + dfmSurchargePercent / 100m);
        decimal totalWithMargin = totalWithDfm * marginMultiplier;

        return Math.Max(totalWithMargin, minimumOrderPrice);
    }
}

public class SlaPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "SLA";

    public decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        decimal marginMultiplier, DfmMetrics? dfm,
        Dictionary<string, string> processParameters)
    {
        decimal totalLayers = boundingBoxZ > 0 ? boundingBoxZ / 0.05m : 2000;
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
        decimal baseTotal = materialCost + machineCost + setupFee;

        decimal dfmSurchargePercent = 0m;
        if (dfm != null)
        {
            if (dfm.ResinTrappingRisk)
            {
                dfmSurchargePercent += 10m;
            }
            if (dfm.SuctionRisk)
            {
                dfmSurchargePercent += 5m;
            }
            if (dfm.ThinWallCount > 0)
            {
                dfmSurchargePercent += Math.Min(dfm.ThinWallCount * 3m, 10m);
            }
        }

        decimal totalWithDfm = baseTotal * (1 + dfmSurchargePercent / 100m);
        decimal totalWithMargin = totalWithDfm * marginMultiplier;

        return Math.Max(totalWithMargin, minimumOrderPrice);
    }
}

public class CncPricingCalculator : IPricingCalculator
{
    public virtual string TechnologyName => "CNC";

    public virtual decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        decimal marginMultiplier, DfmMetrics? dfm,
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
        decimal baseTotal = blockMaterialCost + machineCost + setupFee;

        decimal dfmSurchargePercent = 0m;
        if (dfm != null)
        {
            if (dfm.SharpCornerCount > 0)
            {
                dfmSurchargePercent += Math.Min(dfm.SharpCornerCount * 2m, 25m);
            }
            if (dfm.HasUndercuts)
            {
                dfmSurchargePercent += 15m;
            }
            if (dfm.RequiresEdm)
            {
                dfmSurchargePercent += 20m;
            }
            if (dfm.RequiresGrinding)
            {
                dfmSurchargePercent += 15m;
            }
        }

        decimal totalWithDfm = baseTotal * (1 + dfmSurchargePercent / 100m);
        decimal totalWithMargin = totalWithDfm * marginMultiplier;

        return Math.Max(totalWithMargin, minimumOrderPrice);
    }
}

/// <summary>
/// CNC Milling pricing — same material-removal model as legacy CNC but registered under the CNC_MILL process code.
/// </summary>
public class CncMillPricingCalculator : CncPricingCalculator
{
    public override string TechnologyName => "CNC_MILL";
}

/// <summary>
/// CNC Turning pricing — lower MRR multiplier than milling; turning is a continuous cut so effective
/// chip-removal rates are higher but the geometry complexity surcharge is reduced.
/// </summary>
public class CncTurnPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "CNC_TURN";

    public decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        decimal marginMultiplier, DfmMetrics? dfm,
        Dictionary<string, string> processParameters)
    {
        decimal blockVolume = boundingBoxX * boundingBoxY * boundingBoxZ;
        decimal removalVolume = Math.Max(0, blockVolume - volumeCm3);

        // Turning MRR is typically 1.5–3× milling for the same material
        decimal mrr = 80m;
        if (processParameters.TryGetValue("MRR", out var mrrStr) && decimal.TryParse(mrrStr, out var parsedMrr))
            mrr = parsedMrr;

        decimal machinabilityRating = 1.0m;
        if (processParameters.TryGetValue("MachinabilityRating", out var machRatingStr) && decimal.TryParse(machRatingStr, out var parsedRating))
            machinabilityRating = parsedRating;

        decimal machiningTimeHours = removalVolume / (mrr * machinabilityRating);

        decimal blockMaterialCost = blockVolume * materialCostPerCm3;
        decimal machineCost = machiningTimeHours * machineHourlyRate;
        decimal baseTotal = blockMaterialCost + machineCost + setupFee;

        decimal dfmSurchargePercent = 0m;
        if (dfm != null)
        {
            if (dfm.SharpCornerCount > 0)
                dfmSurchargePercent += Math.Min(dfm.SharpCornerCount * 2m, 15m);
            if (dfm.HasUndercuts)
                dfmSurchargePercent += 20m;
        }

        decimal totalWithDfm = baseTotal * (1 + dfmSurchargePercent / 100m);
        return Math.Max(totalWithDfm * marginMultiplier, minimumOrderPrice);
    }
}

/// <summary>
/// SLS pricing — powder-bed material cost from part volume × density, machine time from build height.
/// Refresh powder usage (unsintered powder) adds ~15% material overhead.
/// </summary>
public class SlsPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "SLS";

    public decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        decimal marginMultiplier, DfmMetrics? dfm,
        Dictionary<string, string> processParameters)
    {
        // SLS: no support material, but ~15% powder refresh overhead
        decimal materialCost = volumeCm3 * materialCostPerCm3 * 1.15m;

        decimal layerHeightMm = 0.1m;
        if (processParameters.TryGetValue("LayerHeight", out var lhStr) && decimal.TryParse(lhStr, out var lhParsed) && lhParsed > 0)
            layerHeightMm = lhParsed;

        decimal totalLayers = boundingBoxZ > 0 ? boundingBoxZ / layerHeightMm : 1000;
        decimal secondsPerLayer = 30m;
        if (processParameters.TryGetValue("SecondsPerLayer", out var splStr) && decimal.TryParse(splStr, out var splParsed))
            secondsPerLayer = splParsed;

        decimal printTimeHours = totalLayers * secondsPerLayer / 3600m;
        decimal machineCost = printTimeHours * machineHourlyRate;
        decimal baseTotal = materialCost + machineCost + setupFee;

        return Math.Max(baseTotal * marginMultiplier, minimumOrderPrice);
    }
}

/// <summary>
/// MJF pricing — similar to SLS (powder bed) but slightly faster scan times and fusing agent adds to material cost.
/// </summary>
public class MjfPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "MJF";

    public decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        decimal marginMultiplier, DfmMetrics? dfm,
        Dictionary<string, string> processParameters)
    {
        // MJF: fusing + detailing agent adds ~20% over powder cost
        decimal materialCost = volumeCm3 * materialCostPerCm3 * 1.20m;

        decimal layerHeightMm = 0.08m;
        if (processParameters.TryGetValue("LayerHeight", out var lhStr) && decimal.TryParse(lhStr, out var lhParsed) && lhParsed > 0)
            layerHeightMm = lhParsed;

        decimal totalLayers = boundingBoxZ > 0 ? boundingBoxZ / layerHeightMm : 1250;
        // MJF is faster per layer than SLS (~20s per layer for standard build)
        decimal secondsPerLayer = 20m;
        if (processParameters.TryGetValue("SecondsPerLayer", out var splStr) && decimal.TryParse(splStr, out var splParsed))
            secondsPerLayer = splParsed;

        decimal printTimeHours = totalLayers * secondsPerLayer / 3600m;
        decimal machineCost = printTimeHours * machineHourlyRate;
        decimal baseTotal = materialCost + machineCost + setupFee;

        return Math.Max(baseTotal * marginMultiplier, minimumOrderPrice);
    }
}

/// <summary>
/// Material Jetting (PolyJet) pricing — very fine layers (14–30 μm), wax support material cost separate.
/// Exposure time is fast due to full-width inkjet array.
/// </summary>
public class MjPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "MJ";

    public decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        decimal marginMultiplier, DfmMetrics? dfm,
        Dictionary<string, string> processParameters)
    {
        decimal materialCost = volumeCm3 * materialCostPerCm3;
        if (processParameters.TryGetValue("SupportMaterialPricePerCm3", out var supportCostStr) && decimal.TryParse(supportCostStr, out var supportCost))
            materialCost += supportVolumeCm3 * supportCost;
        else
            materialCost += supportVolumeCm3 * materialCostPerCm3 * 0.5m;

        decimal layerHeightMm = 0.016m;
        if (processParameters.TryGetValue("LayerHeight", out var lhStr) && decimal.TryParse(lhStr, out var lhParsed) && lhParsed > 0)
            layerHeightMm = lhParsed;

        decimal totalLayers = boundingBoxZ > 0 ? boundingBoxZ / layerHeightMm : 6250;
        // Full-width inkjet array: ~8s per layer regardless of cross-section area
        decimal secondsPerLayer = 8m;
        if (processParameters.TryGetValue("SecondsPerLayer", out var splStr) && decimal.TryParse(splStr, out var splParsed))
            secondsPerLayer = splParsed;

        decimal printTimeHours = totalLayers * secondsPerLayer / 3600m;
        decimal machineCost = printTimeHours * machineHourlyRate;
        decimal baseTotal = materialCost + machineCost + setupFee;

        return Math.Max(baseTotal * marginMultiplier, minimumOrderPrice);
    }
}

/// <summary>
/// Binder Jetting pricing — material cost from part volume; sintering post-process adds significant cost.
/// Sand casting binder jetting uses a flat multiplier for mold cost.
/// </summary>
public class BjPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "BJ";

    public decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        decimal marginMultiplier, DfmMetrics? dfm,
        Dictionary<string, string> processParameters)
    {
        decimal materialCost = volumeCm3 * materialCostPerCm3;

        decimal layerHeightMm = 0.05m;
        if (processParameters.TryGetValue("LayerHeight", out var lhStr) && decimal.TryParse(lhStr, out var lhParsed) && lhParsed > 0)
            layerHeightMm = lhParsed;

        decimal totalLayers = boundingBoxZ > 0 ? boundingBoxZ / layerHeightMm : 2000;
        decimal secondsPerLayer = 15m;
        if (processParameters.TryGetValue("SecondsPerLayer", out var splStr) && decimal.TryParse(splStr, out var splParsed))
            secondsPerLayer = splParsed;

        decimal printTimeHours = totalLayers * secondsPerLayer / 3600m;
        decimal machineCost = printTimeHours * machineHourlyRate;

        // Sintering/infiltration adds ~35% post-processing cost
        decimal sinteringCost = (materialCost + machineCost) * 0.35m;
        decimal baseTotal = materialCost + machineCost + sinteringCost + setupFee;

        return Math.Max(baseTotal * marginMultiplier, minimumOrderPrice);
    }
}

/// <summary>
/// DMLS pricing — metal powder cost from part volume; high machine rate for laser sintering.
/// DFM surcharge for support-heavy geometries (supports are difficult to remove in metal).
/// </summary>
public class DmlsPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "DMLS";

    public decimal Calculate(decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        decimal marginMultiplier, DfmMetrics? dfm,
        Dictionary<string, string> processParameters)
    {
        // Supports in DMLS are solid metal — they cost as much as the part itself
        decimal materialCost = (volumeCm3 + supportVolumeCm3) * materialCostPerCm3;

        decimal layerHeightMm = 0.04m;
        if (processParameters.TryGetValue("LayerHeight", out var lhStr) && decimal.TryParse(lhStr, out var lhParsed) && lhParsed > 0)
            layerHeightMm = lhParsed;

        decimal totalLayers = boundingBoxZ > 0 ? boundingBoxZ / layerHeightMm : 2500;
        // DMLS scan time depends on cross-section; approximate 60s per layer
        decimal secondsPerLayer = 60m;
        if (processParameters.TryGetValue("SecondsPerLayer", out var splStr) && decimal.TryParse(splStr, out var splParsed))
            secondsPerLayer = splParsed;

        decimal printTimeHours = totalLayers * secondsPerLayer / 3600m;
        decimal machineCost = printTimeHours * machineHourlyRate;
        decimal baseTotal = materialCost + machineCost + setupFee;

        decimal dfmSurchargePercent = 0m;
        if (dfm != null)
        {
            // Overhangs requiring supports are expensive in DMLS
            if (dfm.SupportRequired)
                dfmSurchargePercent += 20m;
            if (dfm.ThinWallCount > 0)
                dfmSurchargePercent += Math.Min(dfm.ThinWallCount * 3m, 15m);
        }

        decimal totalWithDfm = baseTotal * (1 + dfmSurchargePercent / 100m);
        return Math.Max(totalWithDfm * marginMultiplier, minimumOrderPrice);
    }
}

/// <summary>
/// Fixed-price calculator for 3D scanning services.
/// Returns the configured minimum order price (2,500 THB for raw STL, 4,500 THB for reverse engineering).
/// The actual tier is determined by the <c>PricingConfiguration.MinimumOrderPrice</c> in the database.
/// </summary>
public sealed class ScanningPricingCalculator : IPricingCalculator
{
    /// <inheritdoc />
    public string TechnologyName => "Scanning";

    /// <summary>
    /// Calculates the price for 3D scanning services.
    /// Scanning is a fixed-price service — the result is always the <paramref name="minimumOrderPrice"/>
    /// configured in the database for the specific scanning tier.
    /// </summary>
    public decimal Calculate(
        decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        decimal marginMultiplier, DfmMetrics? dfm,
        Dictionary<string, string> processParameters)
    {
        return minimumOrderPrice;
    }
}

/// <summary>
/// Fixed-price calculator for 3D design services.
/// Returns the configured minimum order price (500 THB per job).
/// </summary>
public sealed class DesignPricingCalculator : IPricingCalculator
{
    /// <inheritdoc />
    public string TechnologyName => "Design";

    /// <summary>
    /// Calculates the price for 3D design services.
    /// Design is a fixed-price service — the result is always the <paramref name="minimumOrderPrice"/>
    /// configured in the database.
    /// </summary>
    public decimal Calculate(
        decimal volumeCm3, decimal supportVolumeCm3, decimal surfaceAreaCm2,
        decimal boundingBoxX, decimal boundingBoxY, decimal boundingBoxZ,
        decimal materialCostPerCm3, decimal machineHourlyRate, decimal setupFee, decimal minimumOrderPrice,
        decimal marginMultiplier, DfmMetrics? dfm,
        Dictionary<string, string> processParameters)
    {
        return minimumOrderPrice;
    }
}
