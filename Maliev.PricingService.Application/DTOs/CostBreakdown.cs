using System.Text.Json.Serialization;

namespace Maliev.PricingService.Application.DTOs;

public sealed record CostBreakdown(
    decimal MaterialCost,
    decimal SupportMaterialCost,
    decimal MachineTimeCost,
    decimal SetupCost,
    decimal DfmSurcharge,
    decimal ComplexitySurcharge,
    decimal SubtotalBeforeMargin,
    decimal MinimumOrderPriceFloor)
{
    [JsonIgnore]
    public decimal FixedDfmSurcharge { get; init; }
}
