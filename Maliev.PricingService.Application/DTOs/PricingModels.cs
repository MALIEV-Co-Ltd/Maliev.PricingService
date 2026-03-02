namespace Maliev.PricingService.Application.DTOs;

public record PricingRequest
{
    public Guid FileId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid MaterialId { get; init; }
    public string MaterialCode { get; init; } = string.Empty;
    public Guid ManufacturingProcessId { get; init; }
    public string ManufacturingProcessName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Currency { get; init; } = "THB";
    public GeometryMetrics Geometry { get; init; } = new();
    public Guid? CorrelationId { get; init; }
}

public record GeometryMetrics
{
    public decimal VolumeCm3 { get; init; }
    public decimal SupportVolumeCm3 { get; init; }
    public decimal SurfaceAreaCm2 { get; init; }
    public decimal BoundingBoxX { get; init; }
    public decimal BoundingBoxY { get; init; }
    public decimal BoundingBoxZ { get; init; }
    public bool IsManifold { get; init; }
    public int TriangleCount { get; init; }
}

public record PricingResult
{
    public decimal UnitPrice { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal ConfidenceScore { get; init; }
    public string EngineName { get; init; } = string.Empty;
}
