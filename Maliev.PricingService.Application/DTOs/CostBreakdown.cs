namespace Maliev.PricingService.Application.DTOs;

public sealed record CostBreakdown(
    decimal MaterialCost,
    decimal SupportMaterialCost,
    decimal MachineTimeCost,
    decimal SetupCost,
    decimal DfmSurcharge,
    decimal ComplexitySurcharge,
    decimal SubtotalBeforeMargin,
    decimal MinimumOrderPriceFloor);
