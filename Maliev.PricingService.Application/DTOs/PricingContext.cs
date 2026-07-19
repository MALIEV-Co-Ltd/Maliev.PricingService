namespace Maliev.PricingService.Application.DTOs;

public sealed record PricingContext(
    GeometryMetrics Geometry,
    decimal MaterialPricePerCm3,
    decimal MachineHourlyRate,
    decimal SetupFee,
    decimal MinimumOrderPrice,
    DfmMetrics? Dfm,
    IReadOnlyDictionary<string, string> ProcessParameters);
