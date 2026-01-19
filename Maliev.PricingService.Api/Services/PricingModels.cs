namespace Maliev.PricingService.Api.Services;

using Maliev.PricingService.Data.Entities;

/// <summary>
/// Pricing strategy used for calculations.
/// </summary>
public enum PricingStrategy
{
    /// <summary>
    /// Deterministic rule-based calculation.
    /// </summary>
    RuleBased = 1,

    /// <summary>
    /// Machine learning enhanced calculation.
    /// </summary>
    MLEnhanced = 2,

    /// <summary>
    /// Manual pricing by staff.
    /// </summary>
    Manual = 3,

    /// <summary>
    /// Combination of rule-based with ML adjustments.
    /// </summary>
    Hybrid = 4
}

/// <summary>
/// Request for pricing calculation.
/// </summary>
public record PricingRequest
{
    /// <summary>
    /// File ID being priced.
    /// </summary>
    public required Guid FileId { get; init; }

    /// <summary>
    /// Customer ID requesting the price.
    /// </summary>
    public required Guid CustomerId { get; init; }

    /// <summary>
    /// Material ID selected.
    /// </summary>
    public required Guid MaterialId { get; init; }

    /// <summary>
    /// Material code (for denormalization).
    /// </summary>
    public required string MaterialCode { get; init; }

    /// <summary>
    /// Manufacturing process ID selected.
    /// </summary>
    public required Guid ManufacturingProcessId { get; init; }

    /// <summary>
    /// Manufacturing process name (for denormalization).
    /// </summary>
    public required string ManufacturingProcessName { get; init; }

    /// <summary>
    /// Quantity of parts.
    /// </summary>
    public int Quantity { get; init; } = 1;

    /// <summary>
    /// Geometry metrics from file analysis.
    /// </summary>
    public required GeometryMetrics Geometry { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Geometry metrics from file analysis.
/// </summary>
public record GeometryMetrics
{
    /// <summary>
    /// Volume in cubic centimeters.
    /// </summary>
    public required decimal VolumeCm3 { get; init; }

    /// <summary>
    /// Support volume in cubic centimeters.
    /// </summary>
    public required decimal SupportVolumeCm3 { get; init; }

    /// <summary>
    /// Surface area in square centimeters.
    /// </summary>
    public required decimal SurfaceAreaCm2 { get; init; }

    /// <summary>
    /// Bounding box X dimension in millimeters.
    /// </summary>
    public required decimal BoundingBoxX { get; init; }

    /// <summary>
    /// Bounding box Y dimension in millimeters.
    /// </summary>
    public required decimal BoundingBoxY { get; init; }

    /// <summary>
    /// Bounding box Z dimension in millimeters.
    /// </summary>
    public required decimal BoundingBoxZ { get; init; }

    /// <summary>
    /// Whether the mesh is manifold.
    /// </summary>
    public required bool IsManifold { get; init; }

    /// <summary>
    /// Number of triangles in the mesh.
    /// </summary>
    public required int TriangleCount { get; init; }
}

/// <summary>
/// Result of pricing calculation.
/// </summary>
public record PricingResult
{
    /// <summary>
    /// Strategy used for calculation.
    /// </summary>
    public required PricingStrategy Strategy { get; init; }

    /// <summary>
    /// ML model version if ML was used.
    /// </summary>
    public string? MLModelVersion { get; init; }

    /// <summary>
    /// Material cost component.
    /// </summary>
    public required decimal MaterialCost { get; init; }

    /// <summary>
    /// Support material cost component.
    /// </summary>
    public required decimal SupportMaterialCost { get; init; }

    /// <summary>
    /// Machine time cost component.
    /// </summary>
    public required decimal MachineTimeCost { get; init; }

    /// <summary>
    /// Setup cost component.
    /// </summary>
    public required decimal SetupCost { get; init; }

    /// <summary>
    /// Complexity surcharge component.
    /// </summary>
    public required decimal ComplexitySurcharge { get; init; }

    /// <summary>
    /// Subtotal before margin.
    /// </summary>
    public required decimal SubtotalBeforeMargin { get; init; }

    /// <summary>
    /// Margin amount.
    /// </summary>
    public required decimal MarginAmount { get; init; }

    /// <summary>
    /// Unit price (per part).
    /// </summary>
    public required decimal TotalUnitPrice { get; init; }

    /// <summary>
    /// Total price (unit price × quantity).
    /// </summary>
    public required decimal TotalPrice { get; init; }

    /// <summary>
    /// Confidence level (0.0 - 1.0).
    /// </summary>
    public required decimal ConfidenceLevel { get; init; }

    /// <summary>
    /// Calculation duration.
    /// </summary>
    public required TimeSpan CalculationDuration { get; init; }
}
