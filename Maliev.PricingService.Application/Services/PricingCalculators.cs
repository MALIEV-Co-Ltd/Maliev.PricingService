using Maliev.PricingService.Application.DTOs;

namespace Maliev.PricingService.Application.Services;

public interface IPricingCalculator
{
    string TechnologyName { get; }
    IEnumerable<string> Aliases { get; }
    CostBreakdown Calculate(PricingContext ctx);
}

file static class DfmCostAllocator
{
    public static (decimal TotalSurcharge, decimal FixedSurcharge, decimal Subtotal) Allocate(
        decimal variableBase,
        decimal setupFee,
        decimal surchargePercent)
    {
        var surchargeRate = surchargePercent / 100m;
        var fixedSurcharge = setupFee * surchargeRate;
        var totalSurcharge = (variableBase + setupFee) * surchargeRate;
        return (totalSurcharge, fixedSurcharge, variableBase + setupFee + totalSurcharge);
    }
}

public class FdmPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "FDM";
    public IEnumerable<string> Aliases => ["FDM", "Fused Deposition Modeling", "3D Printing (FDM)"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal density = 1.04m;
        if (ctx.ProcessParameters.TryGetValue("Density", out var densityStr) &&
            decimal.TryParse(densityStr, out var parsedDensity))
            density = parsedDensity;

        decimal partMatCost = ctx.Geometry.VolumeCm3 * density * ctx.MaterialPricePerCm3;
        bool hasSupport = ctx.Geometry.SupportVolumeCm3 > 0;
        decimal suppMatCost = hasSupport
            ? ctx.Geometry.SupportVolumeCm3 * 1.2m * density * ctx.MaterialPricePerCm3
            : 0m;

        decimal totalLayers = ctx.Geometry.BoundingBoxZ > 0 ? ctx.Geometry.BoundingBoxZ / 0.2m : 100;
        if (ctx.ProcessParameters.TryGetValue("LayerHeight", out var lhStr) &&
            decimal.TryParse(lhStr, out var lh) && lh > 0)
            totalLayers = ctx.Geometry.BoundingBoxZ / lh;

        decimal flowRate = 15m;
        if (ctx.ProcessParameters.TryGetValue("FlowRate", out var frStr) &&
            decimal.TryParse(frStr, out var parsedFr))
            flowRate = parsedFr;

        decimal minLayerTime = 5m;
        if (ctx.ProcessParameters.TryGetValue("MinLayerTime", out var mltStr) &&
            decimal.TryParse(mltStr, out var parsedMlt))
            minLayerTime = parsedMlt;

        decimal calcLayerTime = ctx.Geometry.VolumeCm3 * 1000 / flowRate / totalLayers;
        decimal effectiveLayerTime = Math.Max(calcLayerTime, minLayerTime);
        decimal printTimeHours = effectiveLayerTime * totalLayers / 3600;

        decimal machineTimeCost = printTimeHours * ctx.MachineHourlyRate;
        decimal variableBaseForDfm = partMatCost + suppMatCost + machineTimeCost;

        decimal dfmSurchargePercent = 0m;
        if (ctx.Dfm != null)
        {
            if (ctx.Dfm.ThinWallCount > 0)
                dfmSurchargePercent += Math.Min(ctx.Dfm.ThinWallCount * 5m, 15m);
            if (ctx.Dfm.SupportRequired && ctx.Dfm.EstimatedSupportVolumeCm3.HasValue &&
                ctx.Dfm.EstimatedSupportVolumeCm3.Value > ctx.Geometry.VolumeCm3 * 0.5m)
                dfmSurchargePercent = Math.Max(dfmSurchargePercent, 15m);
        }

        var dfmCosts = DfmCostAllocator.Allocate(variableBaseForDfm, ctx.SetupFee, dfmSurchargePercent);

        return new CostBreakdown(
            MaterialCost: partMatCost,
            SupportMaterialCost: suppMatCost,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: dfmCosts.TotalSurcharge,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: dfmCosts.Subtotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice)
        {
            FixedDfmSurcharge = dfmCosts.FixedSurcharge
        };
    }
}

public class SlaPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "SLA";
    public IEnumerable<string> Aliases => ["SLA", "Stereolithography", "DLP", "MSLA",
        "3D Printing (SLA)", "3D Printing (SLA/DLP)"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal totalLayers = ctx.Geometry.BoundingBoxZ > 0 ? ctx.Geometry.BoundingBoxZ / 0.05m : 2000;
        if (ctx.ProcessParameters.TryGetValue("LayerHeight", out var lhStr) &&
            decimal.TryParse(lhStr, out var lh) && lh > 0)
            totalLayers = ctx.Geometry.BoundingBoxZ / lh;

        decimal layerExposure = 2.5m;
        if (ctx.ProcessParameters.TryGetValue("LayerExposure", out var expStr) &&
            decimal.TryParse(expStr, out var parsedExp))
            layerExposure = parsedExp;

        decimal liftTime = 3m;
        if (ctx.ProcessParameters.TryGetValue("LiftTime", out var ltStr) &&
            decimal.TryParse(ltStr, out var parsedLt))
            liftTime = parsedLt;

        decimal printTimeHours = totalLayers * (layerExposure + liftTime) / 3600;
        decimal materialCost = ctx.Geometry.VolumeCm3 * ctx.MaterialPricePerCm3;

        decimal suppMatCost = 0m;
        if (ctx.ProcessParameters.TryGetValue("SupportMaterialPricePerCm3", out var scStr) &&
            decimal.TryParse(scStr, out var supportCostPerCm3))
            suppMatCost = ctx.Geometry.SupportVolumeCm3 * supportCostPerCm3;

        decimal machineTimeCost = printTimeHours * ctx.MachineHourlyRate;
        decimal variableBaseForDfm = materialCost + suppMatCost + machineTimeCost;

        decimal dfmSurchargePercent = 0m;
        if (ctx.Dfm != null)
        {
            if (ctx.Dfm.ResinTrappingRisk) dfmSurchargePercent += 10m;
            if (ctx.Dfm.SuctionRisk) dfmSurchargePercent += 5m;
            if (ctx.Dfm.ThinWallCount > 0)
                dfmSurchargePercent += Math.Min(ctx.Dfm.ThinWallCount * 3m, 10m);
        }

        var dfmCosts = DfmCostAllocator.Allocate(variableBaseForDfm, ctx.SetupFee, dfmSurchargePercent);

        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: suppMatCost,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: dfmCosts.TotalSurcharge,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: dfmCosts.Subtotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice)
        {
            FixedDfmSurcharge = dfmCosts.FixedSurcharge
        };
    }
}

public class CncPricingCalculator : IPricingCalculator
{
    public virtual string TechnologyName => "CNC";
    public virtual IEnumerable<string> Aliases => ["CNC", "CNC Machining",
        "Sheet Metal", "Sheet Metal Fabrication", "Injection Molding"];

    public virtual CostBreakdown Calculate(PricingContext ctx)
    {
        // BoundingBox dimensions are in mm; divide by 10 each to get cm, then multiply → cm³
        decimal blockVolume = (ctx.Geometry.BoundingBoxX / 10m)
                            * (ctx.Geometry.BoundingBoxY / 10m)
                            * (ctx.Geometry.BoundingBoxZ / 10m);
        decimal removalVolume = Math.Max(0, blockVolume - ctx.Geometry.VolumeCm3);

        decimal mrr = 50m;
        if (ctx.ProcessParameters.TryGetValue("MRR", out var mrrStr) &&
            decimal.TryParse(mrrStr, out var parsedMrr))
            mrr = parsedMrr;

        decimal machinabilityRating = 1.0m;
        if (ctx.ProcessParameters.TryGetValue("MachinabilityRating", out var mratingStr) &&
            decimal.TryParse(mratingStr, out var parsedRating))
            machinabilityRating = parsedRating;

        decimal machiningTimeHours = removalVolume / (mrr * machinabilityRating);
        decimal machineTimeCost = machiningTimeHours * ctx.MachineHourlyRate;
        decimal materialCost = blockVolume * ctx.MaterialPricePerCm3;

        // Complexity surcharge — unitless surface-area/volume characteristic-length ratio.
        // Sphere ≈ 4.8, cube ≈ 6, complex/lattice > 8.
        decimal complexitySurcharge = 0m;
        if (ctx.Geometry.VolumeCm3 > 0)
        {
            var cbrtV = (decimal)Math.Cbrt((double)ctx.Geometry.VolumeCm3);
            if (cbrtV > 0)
            {
                decimal complexityFactor = ctx.Geometry.SurfaceAreaCm2 / ctx.Geometry.VolumeCm3 / cbrtV;
                decimal complexityThreshold = ctx.ProcessParameters.TryGetValue("ComplexityThreshold", out var ctStr) &&
                    decimal.TryParse(ctStr, out var ct) ? ct : 6.0m;
                decimal surchargeRate = ctx.ProcessParameters.TryGetValue("ComplexitySurchargePercent", out var csStr) &&
                    decimal.TryParse(csStr, out var cs) ? cs / 100m : 0.15m;

                if (complexityFactor > complexityThreshold)
                    complexitySurcharge = machineTimeCost * surchargeRate;
            }
        }

        decimal variableBaseForDfm = materialCost + machineTimeCost;
        decimal dfmSurchargePercent = 0m;
        if (ctx.Dfm != null)
        {
            if (ctx.Dfm.SharpCornerCount > 0)
                dfmSurchargePercent += Math.Min(ctx.Dfm.SharpCornerCount * 2m, 25m);
            if (ctx.Dfm.HasUndercuts) dfmSurchargePercent += 15m;
            if (ctx.Dfm.RequiresEdm) dfmSurchargePercent += 20m;
            if (ctx.Dfm.RequiresGrinding) dfmSurchargePercent += 15m;
        }

        var dfmCosts = DfmCostAllocator.Allocate(variableBaseForDfm, ctx.SetupFee, dfmSurchargePercent);

        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: dfmCosts.TotalSurcharge,
            ComplexitySurcharge: complexitySurcharge,
            SubtotalBeforeMargin: dfmCosts.Subtotal + complexitySurcharge,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice)
        {
            FixedDfmSurcharge = dfmCosts.FixedSurcharge
        };
    }
}

public class CncMillPricingCalculator : CncPricingCalculator
{
    public override string TechnologyName => "CNC_MILL";
    public override IEnumerable<string> Aliases => ["CNC_MILL", "CNC Milling"];
}

public class CncTurnPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "CNC_TURN";
    public IEnumerable<string> Aliases => ["CNC_TURN", "CNC Turning"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal blockVolume = (ctx.Geometry.BoundingBoxX / 10m)
                            * (ctx.Geometry.BoundingBoxY / 10m)
                            * (ctx.Geometry.BoundingBoxZ / 10m);
        decimal removalVolume = Math.Max(0, blockVolume - ctx.Geometry.VolumeCm3);

        decimal mrr = 80m;
        if (ctx.ProcessParameters.TryGetValue("MRR", out var mrrStr) &&
            decimal.TryParse(mrrStr, out var parsedMrr))
            mrr = parsedMrr;

        decimal machinabilityRating = 1.0m;
        if (ctx.ProcessParameters.TryGetValue("MachinabilityRating", out var mratingStr) &&
            decimal.TryParse(mratingStr, out var parsedRating))
            machinabilityRating = parsedRating;

        decimal machiningTimeHours = removalVolume / (mrr * machinabilityRating);
        decimal machineTimeCost = machiningTimeHours * ctx.MachineHourlyRate;
        decimal materialCost = blockVolume * ctx.MaterialPricePerCm3;

        decimal dfmSurchargePercent = 0m;
        if (ctx.Dfm != null)
        {
            if (ctx.Dfm.SharpCornerCount > 0)
                dfmSurchargePercent += Math.Min(ctx.Dfm.SharpCornerCount * 2m, 15m);
            if (ctx.Dfm.HasUndercuts) dfmSurchargePercent += 20m;
        }

        decimal variableBaseForDfm = materialCost + machineTimeCost;
        var dfmCosts = DfmCostAllocator.Allocate(variableBaseForDfm, ctx.SetupFee, dfmSurchargePercent);

        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: dfmCosts.TotalSurcharge,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: dfmCosts.Subtotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice)
        {
            FixedDfmSurcharge = dfmCosts.FixedSurcharge
        };
    }
}

public class SlsPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "SLS";
    public IEnumerable<string> Aliases => ["SLS", "3D Printing (SLS)"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal materialCost = ctx.Geometry.VolumeCm3 * ctx.MaterialPricePerCm3 * 1.15m;

        decimal layerHeightMm = 0.1m;
        if (ctx.ProcessParameters.TryGetValue("LayerHeight", out var lhStr) &&
            decimal.TryParse(lhStr, out var lh) && lh > 0)
            layerHeightMm = lh;

        decimal totalLayers = ctx.Geometry.BoundingBoxZ > 0 ? ctx.Geometry.BoundingBoxZ / layerHeightMm : 1000;
        decimal secondsPerLayer = 30m;
        if (ctx.ProcessParameters.TryGetValue("SecondsPerLayer", out var splStr) &&
            decimal.TryParse(splStr, out var spl))
            secondsPerLayer = spl;

        decimal printTimeHours = totalLayers * secondsPerLayer / 3600m;
        decimal machineTimeCost = printTimeHours * ctx.MachineHourlyRate;
        decimal subTotal = materialCost + machineTimeCost + ctx.SetupFee;

        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

public class MjfPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "MJF";
    public IEnumerable<string> Aliases => ["MJF", "3D Printing (MJF)"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal materialCost = ctx.Geometry.VolumeCm3 * ctx.MaterialPricePerCm3 * 1.20m;

        decimal layerHeightMm = 0.08m;
        if (ctx.ProcessParameters.TryGetValue("LayerHeight", out var lhStr) &&
            decimal.TryParse(lhStr, out var lh) && lh > 0)
            layerHeightMm = lh;

        decimal totalLayers = ctx.Geometry.BoundingBoxZ > 0 ? ctx.Geometry.BoundingBoxZ / layerHeightMm : 1250;
        decimal secondsPerLayer = 20m;
        if (ctx.ProcessParameters.TryGetValue("SecondsPerLayer", out var splStr) &&
            decimal.TryParse(splStr, out var spl))
            secondsPerLayer = spl;

        decimal printTimeHours = totalLayers * secondsPerLayer / 3600m;
        decimal machineTimeCost = printTimeHours * ctx.MachineHourlyRate;
        decimal subTotal = materialCost + machineTimeCost + ctx.SetupFee;

        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

public class MjPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "MJ";
    public IEnumerable<string> Aliases => ["MJ", "3D Printing (Material Jetting)"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal materialCost = ctx.Geometry.VolumeCm3 * ctx.MaterialPricePerCm3;
        decimal suppMatCost = ctx.ProcessParameters.TryGetValue("SupportMaterialPricePerCm3", out var scStr) &&
            decimal.TryParse(scStr, out var sc)
            ? ctx.Geometry.SupportVolumeCm3 * sc
            : ctx.Geometry.SupportVolumeCm3 * ctx.MaterialPricePerCm3 * 0.5m;

        decimal layerHeightMm = 0.016m;
        if (ctx.ProcessParameters.TryGetValue("LayerHeight", out var lhStr) &&
            decimal.TryParse(lhStr, out var lh) && lh > 0)
            layerHeightMm = lh;

        decimal totalLayers = ctx.Geometry.BoundingBoxZ > 0 ? ctx.Geometry.BoundingBoxZ / layerHeightMm : 6250;
        decimal secondsPerLayer = 8m;
        if (ctx.ProcessParameters.TryGetValue("SecondsPerLayer", out var splStr) &&
            decimal.TryParse(splStr, out var spl))
            secondsPerLayer = spl;

        decimal printTimeHours = totalLayers * secondsPerLayer / 3600m;
        decimal machineTimeCost = printTimeHours * ctx.MachineHourlyRate;
        decimal subTotal = materialCost + suppMatCost + machineTimeCost + ctx.SetupFee;

        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: suppMatCost,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

public class BjPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "BJ";
    public IEnumerable<string> Aliases => ["BJ", "3D Printing (Binder Jetting)"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal materialCost = ctx.Geometry.VolumeCm3 * ctx.MaterialPricePerCm3;

        decimal layerHeightMm = 0.05m;
        if (ctx.ProcessParameters.TryGetValue("LayerHeight", out var lhStr) &&
            decimal.TryParse(lhStr, out var lh) && lh > 0)
            layerHeightMm = lh;

        decimal totalLayers = ctx.Geometry.BoundingBoxZ > 0 ? ctx.Geometry.BoundingBoxZ / layerHeightMm : 2000;
        decimal secondsPerLayer = 15m;
        if (ctx.ProcessParameters.TryGetValue("SecondsPerLayer", out var splStr) &&
            decimal.TryParse(splStr, out var spl))
            secondsPerLayer = spl;

        decimal printTimeHours = totalLayers * secondsPerLayer / 3600m;
        decimal baseMachineTime = printTimeHours * ctx.MachineHourlyRate;
        // Sintering/infiltration adds ~35% post-processing cost (rolled into machine time)
        decimal sinteringCost = (materialCost + baseMachineTime) * 0.35m;
        decimal machineTimeCost = baseMachineTime + sinteringCost;
        decimal subTotal = materialCost + machineTimeCost + ctx.SetupFee;

        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

public class DmlsPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "DMLS";
    public IEnumerable<string> Aliases => ["DMLS", "3D Printing (DMLS)"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal materialCost = ctx.Geometry.VolumeCm3 * ctx.MaterialPricePerCm3;
        decimal suppMatCost = ctx.Geometry.SupportVolumeCm3 * ctx.MaterialPricePerCm3;

        decimal layerHeightMm = 0.04m;
        if (ctx.ProcessParameters.TryGetValue("LayerHeight", out var lhStr) &&
            decimal.TryParse(lhStr, out var lh) && lh > 0)
            layerHeightMm = lh;

        decimal totalLayers = ctx.Geometry.BoundingBoxZ > 0 ? ctx.Geometry.BoundingBoxZ / layerHeightMm : 2500;
        decimal secondsPerLayer = 60m;
        if (ctx.ProcessParameters.TryGetValue("SecondsPerLayer", out var splStr) &&
            decimal.TryParse(splStr, out var spl))
            secondsPerLayer = spl;

        decimal printTimeHours = totalLayers * secondsPerLayer / 3600m;
        decimal machineTimeCost = printTimeHours * ctx.MachineHourlyRate;
        decimal variableBaseForDfm = materialCost + suppMatCost + machineTimeCost;

        decimal dfmSurchargePercent = 0m;
        if (ctx.Dfm != null)
        {
            if (ctx.Dfm.SupportRequired) dfmSurchargePercent += 20m;
            if (ctx.Dfm.ThinWallCount > 0)
                dfmSurchargePercent += Math.Min(ctx.Dfm.ThinWallCount * 3m, 15m);
        }

        var dfmCosts = DfmCostAllocator.Allocate(variableBaseForDfm, ctx.SetupFee, dfmSurchargePercent);

        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: suppMatCost,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: dfmCosts.TotalSurcharge,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: dfmCosts.Subtotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice)
        {
            FixedDfmSurcharge = dfmCosts.FixedSurcharge
        };
    }
}

public sealed class ScanningPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "Scanning";
    public IEnumerable<string> Aliases => ["3D Scanning", "3D Scanning (Raw STL)",
        "3D Scanning + Reverse Engineering", "Scanning"];

    public CostBreakdown Calculate(PricingContext ctx) =>
        new(0m, 0m, 0m, 0m, 0m, 0m, 0m, ctx.MinimumOrderPrice);
}

public sealed class DesignPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "Design";
    public IEnumerable<string> Aliases => ["3D Design", "Design"];

    public CostBreakdown Calculate(PricingContext ctx) =>
        new(0m, 0m, 0m, 0m, 0m, 0m, 0m, ctx.MinimumOrderPrice);
}

// ── Phase 4: New per-volume / per-area calculators ────────────────────────────

/// <summary>
/// Sheet Metal fabrication.
/// Formula: (sheetAreaCm2 × pricePerCm2 × thicknessFactor) + (cutLengthMm × cutPricePerMm) + (bendCount × pricePerBend) + setup.
/// Config mapping:
///   MaterialPricePerCm3 → price per cm² of sheet (thickness already in material price)
///   SupportMaterialPricePerCm3 → cut price per mm
///   ProcessParameters["BendCount"]  → number of bends
///   ProcessParameters["CutLengthMm"] → cut perimeter length
///   MachineHourlyRate → used for blanking time if neither cut nor bend params given
/// </summary>
public sealed class SheetMetalPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "SHEET_METAL";
    public IEnumerable<string> Aliases => ["SHEET_METAL", "Sheet Metal", "Sheet Metal Fabrication", "Laser Cutting"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        // Surface area gives the sheet area; if not provided fall back to bounding-box XY
        decimal sheetAreaCm2 = ctx.Geometry.SurfaceAreaCm2 > 0
            ? ctx.Geometry.SurfaceAreaCm2
            : ctx.Geometry.BoundingBoxX * ctx.Geometry.BoundingBoxY / 100m; // mm² → cm²

        decimal materialCost = sheetAreaCm2 * ctx.MaterialPricePerCm3;

        decimal cutPricePerMm = ctx.ProcessParameters.TryGetValue("SupportMaterialPricePerCm3", out var cpStr)
            && decimal.TryParse(cpStr, out var cp) ? cp : 0.5m;
        decimal cutLengthMm = ctx.ProcessParameters.TryGetValue("CutLengthMm", out var clStr)
            && decimal.TryParse(clStr, out var cl) ? cl : 0m;
        decimal bendPriceEach = ctx.ProcessParameters.TryGetValue("BendPriceEach", out var bpStr)
            && decimal.TryParse(bpStr, out var bp) ? bp : 50m;
        int bendCount = ctx.ProcessParameters.TryGetValue("BendCount", out var bcStr)
            && int.TryParse(bcStr, out var bc) ? bc : 0;

        decimal machineTimeCost = cutLengthMm * cutPricePerMm + bendCount * bendPriceEach;
        if (machineTimeCost == 0m)
        {
            // Fallback: estimate 1 min per 10 cm² of sheet
            decimal estimatedMinutes = sheetAreaCm2 / 10m;
            machineTimeCost = estimatedMinutes / 60m * ctx.MachineHourlyRate;
        }

        decimal subTotal = materialCost + machineTimeCost + ctx.SetupFee;
        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

/// <summary>
/// Injection Molding (machine-based, replaces the mis-dispatched CNC path).
/// Formula: tooling/N + V × resinCost × (1+scrapPct) + cycleSec × machineRate/3600 + setup.
/// Config mapping:
///   MaterialPricePerCm3 → resin cost per cm³
///   MachineHourlyRate   → injection moulding machine hourly rate
///   ProcessParameters["ToolingCostFlat"] → amortised tooling (split by Quantity at orchestrator level)
///   ProcessParameters["ScrapPct"]        → waste fraction (default 0.08 = 8%)
///   ProcessParameters["CycleSecondsPerCm3"] → cycle time per cm³ of part volume
/// </summary>
public sealed class InjectionMouldingPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "INJ_MOULD";
    public IEnumerable<string> Aliases => ["INJ_MOULD", "Injection Molding", "Injection Moulding", "IM"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal scrapPct = ctx.ProcessParameters.TryGetValue("ScrapPct", out var spStr)
            && decimal.TryParse(spStr, out var sp) ? sp : 0.08m;
        decimal cycleSecondsPerCm3 = ctx.ProcessParameters.TryGetValue("CycleSecondsPerCm3", out var csStr)
            && decimal.TryParse(csStr, out var cs) ? cs : 1.5m;
        decimal toolingCostFlat = ctx.ProcessParameters.TryGetValue("ToolingCostFlat", out var tcStr)
            && decimal.TryParse(tcStr, out var tc) ? tc : 0m;

        decimal materialCost = ctx.Geometry.VolumeCm3 * ctx.MaterialPricePerCm3 * (1m + scrapPct);
        decimal cycleSeconds = cycleSecondsPerCm3 * ctx.Geometry.VolumeCm3;
        decimal machineTimeCost = cycleSeconds / 3600m * ctx.MachineHourlyRate;

        // toolingCostFlat is a per-unit amortisation amount set by upstream (quantity / fixed tooling cost)
        decimal subTotal = toolingCostFlat + materialCost + machineTimeCost + ctx.SetupFee;
        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee + toolingCostFlat,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

/// <summary>
/// Silicone Casting.
/// Formula: mouldFee + V × siliconeCostPerCm3 × (1+wastePct) + cureHours × labourRate + setup.
/// Config mapping:
///   MaterialPricePerCm3 → silicone cost per cm³
///   MachineHourlyRate   → labour rate per hour
///   ProcessParameters["MouldFeeFlat"]   → mould cost (fixed per job, not amortised at this level)
///   ProcessParameters["WastePct"]       → silicone waste fraction (default 0.20 = 20%)
///   ProcessParameters["CureHoursPerCm3"] → cure hours per cm³ of part volume
/// </summary>
public sealed class SiliconeCastingPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "SI_CAST";
    public IEnumerable<string> Aliases => ["SI_CAST", "Silicone Casting", "RTV Silicone"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal wastePct = ctx.ProcessParameters.TryGetValue("WastePct", out var wpStr)
            && decimal.TryParse(wpStr, out var wp) ? wp : 0.20m;
        decimal cureHoursPerCm3 = ctx.ProcessParameters.TryGetValue("CureHoursPerCm3", out var chStr)
            && decimal.TryParse(chStr, out var ch) ? ch : 0.05m;
        decimal mouldFeeFlat = ctx.ProcessParameters.TryGetValue("MouldFeeFlat", out var mfStr)
            && decimal.TryParse(mfStr, out var mf) ? mf : 0m;

        decimal materialCost = ctx.Geometry.VolumeCm3 * ctx.MaterialPricePerCm3 * (1m + wastePct);
        decimal cureHours = cureHoursPerCm3 * ctx.Geometry.VolumeCm3;
        decimal machineTimeCost = cureHours * ctx.MachineHourlyRate;

        decimal subTotal = mouldFeeFlat + materialCost + machineTimeCost + ctx.SetupFee;
        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee + mouldFeeFlat,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

/// <summary>
/// Manual Injection Molding (pneumatic plunger).
/// Differs from machine IM: cycle time is manual-paced; operator rate added to machine rate.
/// Formula: tooling/N + V × resinCost × (1+scrapPct) + cycleSec/3600 × (machineRate+operatorRate) + setup.
/// Config mapping:
///   MaterialPricePerCm3 → resin cost per cm³
///   MachineHourlyRate   → machine rate (operator rate added via ProcessParameters)
///   ProcessParameters["OperatorRatePerHour"] → operator rate (default 200 THB/h)
///   ProcessParameters["ToolingCostFlat"]     → tooling amortisation
///   ProcessParameters["ScrapPct"]            → scrap fraction (default 0.10)
///   ProcessParameters["CycleSecondsPerCm3"]  → cycle time per cm³
/// </summary>
public sealed class ManualInjectionMouldingPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "MAN_IM";
    public IEnumerable<string> Aliases => ["MAN_IM", "Manual Injection Molding", "Manual Injection Moulding", "Pneumatic IM"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal scrapPct = ctx.ProcessParameters.TryGetValue("ScrapPct", out var spStr)
            && decimal.TryParse(spStr, out var sp) ? sp : 0.10m;
        decimal cycleSecondsPerCm3 = ctx.ProcessParameters.TryGetValue("CycleSecondsPerCm3", out var csStr)
            && decimal.TryParse(csStr, out var cs) ? cs : 2.0m;
        decimal toolingCostFlat = ctx.ProcessParameters.TryGetValue("ToolingCostFlat", out var tcStr)
            && decimal.TryParse(tcStr, out var tc) ? tc : 0m;
        decimal operatorRatePerHour = ctx.ProcessParameters.TryGetValue("OperatorRatePerHour", out var orStr)
            && decimal.TryParse(orStr, out var or_) ? or_ : 200m;

        decimal materialCost = ctx.Geometry.VolumeCm3 * ctx.MaterialPricePerCm3 * (1m + scrapPct);
        decimal cycleSeconds = cycleSecondsPerCm3 * ctx.Geometry.VolumeCm3;
        decimal machineTimeCost = cycleSeconds / 3600m * (ctx.MachineHourlyRate + operatorRatePerHour);

        decimal subTotal = toolingCostFlat + materialCost + machineTimeCost + ctx.SetupFee;
        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee + toolingCostFlat,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

/// <summary>
/// Painting (spray / brush / powder coating).
/// Formula: (SA/10000 m²) × pricePerSqm × layerCount + maskingFee + cureHours × labourRate + setup.
/// Config mapping:
///   MaterialPricePerCm3 → price per cm² of surface treated
///     (so total = SA × MaterialPricePerCm3; pricing team converts from m²-based tariff)
///   MachineHourlyRate   → labour rate per hour
///   ProcessParameters["LayerCount"]   → number of coats (default 2)
///   ProcessParameters["MaskingFeeFlat"] → flat masking fee
///   ProcessParameters["CureHoursPerSqm"] → cure time per cm² (default 0.001 h/cm²)
/// </summary>
public sealed class PaintingPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "PAINT";
    public IEnumerable<string> Aliases => ["PAINT", "Painting", "Coating", "Powder Coating", "Spray Painting"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        int layerCount = ctx.ProcessParameters.TryGetValue("LayerCount", out var lcStr)
            && int.TryParse(lcStr, out var lc) ? lc : 2;
        decimal maskingFeeFlat = ctx.ProcessParameters.TryGetValue("MaskingFeeFlat", out var mfStr)
            && decimal.TryParse(mfStr, out var mf) ? mf : 0m;
        decimal cureHoursPerCm2 = ctx.ProcessParameters.TryGetValue("CureHoursPerSqm", out var chStr)
            && decimal.TryParse(chStr, out var ch) ? ch : 0.001m;

        decimal surfaceAreaCm2 = ctx.Geometry.SurfaceAreaCm2 > 0
            ? ctx.Geometry.SurfaceAreaCm2
            : 2m * (ctx.Geometry.BoundingBoxX * ctx.Geometry.BoundingBoxY
                  + ctx.Geometry.BoundingBoxY * ctx.Geometry.BoundingBoxZ
                  + ctx.Geometry.BoundingBoxX * ctx.Geometry.BoundingBoxZ) / 100m;

        decimal materialCost = surfaceAreaCm2 * ctx.MaterialPricePerCm3 * layerCount;
        decimal cureHours = cureHoursPerCm2 * surfaceAreaCm2;
        decimal machineTimeCost = cureHours * ctx.MachineHourlyRate + maskingFeeFlat;

        decimal subTotal = materialCost + machineTimeCost + ctx.SetupFee;
        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

/// <summary>
/// Surface Finishing (anodising, polishing, bead blasting, etc.).
/// Formula: (SA / 10000 m²) × pricePerSqm + setup.
/// Config mapping:
///   MaterialPricePerCm3 → price per cm² of surface treated
///   ProcessParameters["FinishMultiplier"] → area multiplier for complex surfaces (default 1.0)
/// </summary>
public sealed class SurfaceFinishingPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "SURF_FIN";
    public IEnumerable<string> Aliases => ["SURF_FIN", "Surface Finishing", "Anodising", "Anodizing",
        "Polishing", "Bead Blasting", "Sand Blasting", "Surface Treatment"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal finishMultiplier = ctx.ProcessParameters.TryGetValue("FinishMultiplier", out var fmStr)
            && decimal.TryParse(fmStr, out var fm) ? fm : 1.0m;

        decimal surfaceAreaCm2 = ctx.Geometry.SurfaceAreaCm2 > 0
            ? ctx.Geometry.SurfaceAreaCm2
            : 2m * (ctx.Geometry.BoundingBoxX * ctx.Geometry.BoundingBoxY
                  + ctx.Geometry.BoundingBoxY * ctx.Geometry.BoundingBoxZ
                  + ctx.Geometry.BoundingBoxX * ctx.Geometry.BoundingBoxZ) / 100m;

        decimal materialCost = surfaceAreaCm2 * ctx.MaterialPricePerCm3 * finishMultiplier;
        decimal subTotal = materialCost + ctx.SetupFee;
        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: 0m,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

// ── Phase 5: Per-time / per-feature calculators + China Outsourcing ───────────

/// <summary>
/// EDM (Electrical Discharge Machining — standalone).
/// Formula: (removalV / mrrCm3PerHour) × machineRate + electrodeCount × electrodeCost + setup.
/// Config mapping:
///   MaterialPricePerCm3 → not used directly (EDM removes material; removal volume = VolumeCm3)
///   MachineHourlyRate   → EDM machine hourly rate
///   ProcessParameters["MrrCm3PerHour"]     → material removal rate (default 2.0 cm³/h)
///   ProcessParameters["ElectrodeCount"]    → number of electrodes used (from request)
///   ProcessParameters["ElectrodeCostFlat"] → cost per electrode (default 500 THB)
/// </summary>
public sealed class EdmPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "EDM";
    public IEnumerable<string> Aliases => ["EDM", "Electrical Discharge Machining", "Wire EDM", "Sink EDM"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal mrrCm3PerHour = ctx.ProcessParameters.TryGetValue("MrrCm3PerHour", out var mrStr)
            && decimal.TryParse(mrStr, out var mr) ? mr : 2.0m;
        decimal electrodeCostFlat = ctx.ProcessParameters.TryGetValue("ElectrodeCostFlat", out var ecStr)
            && decimal.TryParse(ecStr, out var ec) ? ec : 500m;
        int electrodeCount = ctx.ProcessParameters.TryGetValue("ElectrodeCount", out var enStr)
            && int.TryParse(enStr, out var en) ? en : 1;

        decimal removalVolume = ctx.Geometry.VolumeCm3 > 0 ? ctx.Geometry.VolumeCm3 : 1m;
        decimal machineHours = mrrCm3PerHour > 0 ? removalVolume / mrrCm3PerHour : 0m;
        decimal machineTimeCost = machineHours * ctx.MachineHourlyRate;
        decimal electrodeCost = electrodeCount * electrodeCostFlat;

        decimal subTotal = machineTimeCost + electrodeCost + ctx.SetupFee;
        return new CostBreakdown(
            MaterialCost: electrodeCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

/// <summary>
/// Grinding (standalone surface/cylindrical grinding).
/// Formula: (SA / 10000 m²) × pricePerSqmGrind × passCount + setup.
/// Config mapping:
///   MaterialPricePerCm3 → price per cm² of surface ground
///   ProcessParameters["PassCount"] → grinding passes (default 2)
/// </summary>
public sealed class GrindingPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "GRIND";
    public IEnumerable<string> Aliases => ["GRIND", "Grinding", "Surface Grinding", "Cylindrical Grinding"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        int passCount = ctx.ProcessParameters.TryGetValue("PassCount", out var pcStr)
            && int.TryParse(pcStr, out var pc) ? pc : 2;

        decimal surfaceAreaCm2 = ctx.Geometry.SurfaceAreaCm2 > 0
            ? ctx.Geometry.SurfaceAreaCm2
            : ctx.Geometry.BoundingBoxX * ctx.Geometry.BoundingBoxY / 100m;

        decimal machineTimeCost = surfaceAreaCm2 * ctx.MaterialPricePerCm3 * passCount;
        decimal subTotal = machineTimeCost + ctx.SetupFee;
        return new CostBreakdown(
            MaterialCost: 0m,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

/// <summary>
/// Welding.
/// Formula: weldLengthMm × pricePerMm × passCount + prepHours × labourRate + setup.
/// Config mapping:
///   MaterialPricePerCm3 → price per mm of weld (repurposed)
///   MachineHourlyRate   → labour rate
///   ProcessParameters["WeldLengthMm"]         → weld length (from request)
///   ProcessParameters["PassCount"]            → weld passes (default 1)
///   ProcessParameters["PreparationHoursFlat"] → prep time in hours (default 0.5)
/// </summary>
public sealed class WeldingPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "WELD";
    public IEnumerable<string> Aliases => ["WELD", "Welding", "TIG Welding", "MIG Welding", "Spot Welding"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal weldLengthMm = ctx.ProcessParameters.TryGetValue("WeldLengthMm", out var wlStr)
            && decimal.TryParse(wlStr, out var wl) ? wl : 0m;
        int passCount = ctx.ProcessParameters.TryGetValue("PassCount", out var pcStr)
            && int.TryParse(pcStr, out var pc) ? pc : 1;
        decimal prepHoursFlat = ctx.ProcessParameters.TryGetValue("PreparationHoursFlat", out var phStr)
            && decimal.TryParse(phStr, out var ph) ? ph : 0.5m;

        decimal weldCost = weldLengthMm * ctx.MaterialPricePerCm3 * passCount;
        decimal prepCost = prepHoursFlat * ctx.MachineHourlyRate;
        decimal machineTimeCost = weldCost + prepCost;

        decimal subTotal = machineTimeCost + ctx.SetupFee;
        return new CostBreakdown(
            MaterialCost: 0m,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

/// <summary>
/// Standard Metal Fabrications (cut-to-size, simple bending, basic structural work).
/// Formula: weightKg × pricePerKgFab + cutLengthMm × cutPricePerMm + bendCount × pricePerBend + setup.
/// Config mapping:
///   MaterialPricePerCm3   → price per kg of fabricated material (repurposed)
///   SupportMaterialPricePerCm3 → cut price per mm
///   ProcessParameters["BendPriceEach"] → price per bend (default 80 THB)
///   ProcessParameters["WeightKg"]     → part weight (from request)
///   ProcessParameters["CutLengthMm"]  → total cut length (from request)
///   ProcessParameters["BendCount"]    → number of bends (from request)
/// </summary>
public sealed class StdMetalFabPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "STD_METAL_FAB";
    public IEnumerable<string> Aliases => ["STD_METAL_FAB", "Standard Metal Fabrication", "Metal Fabrication", "Plate Cutting"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal weightKg = ctx.ProcessParameters.TryGetValue("WeightKg", out var wkStr)
            && decimal.TryParse(wkStr, out var wk) ? wk : 0m;
        decimal cutLengthMm = ctx.ProcessParameters.TryGetValue("CutLengthMm", out var clStr)
            && decimal.TryParse(clStr, out var cl) ? cl : 0m;
        int bendCount = ctx.ProcessParameters.TryGetValue("BendCount", out var bcStr)
            && int.TryParse(bcStr, out var bc) ? bc : 0;
        decimal bendPriceEach = ctx.ProcessParameters.TryGetValue("BendPriceEach", out var bpStr)
            && decimal.TryParse(bpStr, out var bp) ? bp : 80m;
        decimal cutPricePerMm = ctx.ProcessParameters.TryGetValue("SupportMaterialPricePerCm3", out var cpStr)
            && decimal.TryParse(cpStr, out var cp) ? cp : 0.3m;

        decimal materialCost = weightKg * ctx.MaterialPricePerCm3;
        decimal machineTimeCost = cutLengthMm * cutPricePerMm + bendCount * bendPriceEach;

        decimal subTotal = materialCost + machineTimeCost + ctx.SetupFee;
        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: machineTimeCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

/// <summary>
/// Deviation Analysis (scan-to-CAD comparison / GD&T reporting).
/// Tier-priced: effectively a fixed-fee service captured via MinimumOrderPrice.
/// Optional formula for large scans: pointCount × pricePerMillionPoints + analystHours × labourRate + setup.
/// Config mapping:
///   MaterialPricePerCm3 → price per million scan points (set low; most jobs hit floor)
///   MachineHourlyRate   → analyst hourly rate
///   ProcessParameters["PointCountThousands"] → scan point count in thousands (from request)
///   ProcessParameters["AnalystHoursFlat"]    → analyst hours (default 1.0)
/// </summary>
public sealed class DeviationAnalysisPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "DEV_ANALYSIS";
    public IEnumerable<string> Aliases => ["DEV_ANALYSIS", "Deviation Analysis", "Scan to CAD", "GD&T Report"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        decimal pointCountThousands = ctx.ProcessParameters.TryGetValue("PointCountThousands", out var ptStr)
            && decimal.TryParse(ptStr, out var pt) ? pt : 0m;
        decimal analystHoursFlat = ctx.ProcessParameters.TryGetValue("AnalystHoursFlat", out var ahStr)
            && decimal.TryParse(ahStr, out var ah) ? ah : 1.0m;

        // Points in millions × price per million
        decimal scanDataCost = pointCountThousands / 1000m * ctx.MaterialPricePerCm3;
        decimal analystCost = analystHoursFlat * ctx.MachineHourlyRate;
        decimal subTotal = scanDataCost + analystCost + ctx.SetupFee;

        return new CostBreakdown(
            MaterialCost: scanDataCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: analystCost,
            SetupCost: ctx.SetupFee,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}

/// <summary>
/// China Outsourcing (2× markup on vendor quote).
/// Returns VendorQuoteThb (already converted from vendor currency) × markupMultiplier + shippingFlat + setup.
/// NOTE: The orchestrator must convert VendorQuoteAmount from VendorQuoteCurrency to THB BEFORE calling
/// this calculator. The calculator receives the THB-equivalent in ProcessParameters["VendorQuoteThb"].
/// If that key is absent, SubtotalBeforeMargin is 0 and EngineName signals "ChinaOutsourcing-NoVendorQuote".
/// </summary>
public sealed class ChinaOutsourcingPricingCalculator : IPricingCalculator
{
    public string TechnologyName => "CHINA_OS";
    public IEnumerable<string> Aliases => ["CHINA_OS", "China Outsourcing", "Outsourcing", "OEM China"];

    public CostBreakdown Calculate(PricingContext ctx)
    {
        // VendorQuoteAmount arrives pre-converted to THB by the orchestrator
        if (!ctx.ProcessParameters.TryGetValue("VendorQuoteAmount", out var vqStr)
            || !decimal.TryParse(vqStr, out var vendorQuoteThb)
            || vendorQuoteThb <= 0m)
        {
            return new CostBreakdown(0m, 0m, 0m, ctx.SetupFee, 0m, 0m, ctx.SetupFee, ctx.MinimumOrderPrice);
        }

        decimal markupMultiplier = ctx.ProcessParameters.TryGetValue("VendorQuoteMarkupOverride", out var moStr)
            && decimal.TryParse(moStr, out var mo) && mo > 0m ? mo
            : ctx.MaterialPricePerCm3 > 0m ? ctx.MaterialPricePerCm3 // config.MaterialPricePerCm3 stores markup multiplier for CHINA_OS
            : 2.0m;

        decimal shippingFlat = ctx.ProcessParameters.TryGetValue("ShippingFlatThb", out var sfStr)
            && decimal.TryParse(sfStr, out var sf) ? sf : 0m;

        decimal materialCost = vendorQuoteThb * markupMultiplier;
        decimal subTotal = materialCost + shippingFlat + ctx.SetupFee;

        return new CostBreakdown(
            MaterialCost: materialCost,
            SupportMaterialCost: 0m,
            MachineTimeCost: 0m,
            SetupCost: ctx.SetupFee + shippingFlat,
            DfmSurcharge: 0m,
            ComplexitySurcharge: 0m,
            SubtotalBeforeMargin: subTotal,
            MinimumOrderPriceFloor: ctx.MinimumOrderPrice);
    }
}
