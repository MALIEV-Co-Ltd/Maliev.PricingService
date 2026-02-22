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
    /// Manual pricing by staff.
    /// </summary>
    Manual = 3
}

/// <summary>
/// Manufacturing technology selected by the customer.
/// </summary>
public enum ManufacturingTechnology
{
    /// <summary>Fused Deposition Modeling.</summary>
    Fdm = 1,
    /// <summary>Stereolithography.</summary>
    Sla = 2,
    /// <summary>CNC Machining.</summary>
    Cnc = 3,
    /// <summary>3D Scanning.</summary>
    Scanning = 4,
    /// <summary>3D Design.</summary>
    Design = 5
}

/// <summary>
/// Material data properties for pricing.
/// </summary>
public record MaterialData
{
    /// <summary>Density in g/cm³.</summary>
    public decimal Density { get; init; }
    /// <summary>Cost per kg (THB/kg).</summary>
    public decimal CostPerKg { get; init; }

    /// <summary>FDM volumetric flow rate (mm³/s).</summary>
    public decimal FdmVolumetricFlowRate { get; init; }
    /// <summary>FDM minimum layer time (seconds).</summary>
    public decimal FdmMinLayerTime { get; init; }

    /// <summary>SLA layer exposure time (seconds).</summary>
    public decimal SlaLayerExposure { get; init; }
    /// <summary>SLA lift time (seconds).</summary>
    public decimal SlaLiftTime { get; init; }

    /// <summary>CNC machinability rating (0.0-1.0).</summary>
    public decimal CncMachinabilityRating { get; init; }
}

/// <summary>
/// Machine rates and setup fees from configuration.
/// </summary>
public record MachineRates
{
    /// <summary>FDM machine hourly rate (THB/hour).</summary>
    public decimal FdmMachineHourlyRate { get; init; }
    /// <summary>FDM setup fee (THB).</summary>
    public decimal FdmSetupFee { get; init; }

    /// <summary>SLA machine hourly rate (THB/hour).</summary>
    public decimal SlaMachineHourlyRate { get; init; }
    /// <summary>SLA setup fee (THB).</summary>
    public decimal SlaSetupFee { get; init; }

    /// <summary>CNC machine hourly rate (THB/hour).</summary>
    public decimal CncMachineHourlyRate { get; init; }
    /// <summary>CNC setup fee (THB).</summary>
    public decimal CncSetupFee { get; init; }
    /// <summary>CNC material removal rate (mm³/hour).</summary>
    public decimal CncMaterialRemovalRate { get; init; }
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
    /// Manufacturing technology selected.
    /// </summary>
    public required ManufacturingTechnology Technology { get; init; }

    /// <summary>
    /// Manufacturing process ID selected (backward compatibility).
    /// </summary>
    public Guid? ManufacturingProcessId { get; init; }

    /// <summary>
    /// Manufacturing process name (backward compatibility).
    /// </summary>
    public string? ManufacturingProcessName { get; init; }

    /// <summary>
    /// Layer height in millimeters.
    /// </summary>
    public decimal LayerHeightMm { get; init; } = 0.2m;

    /// <summary>
    /// Whether support structures are enabled.
    /// </summary>
    public bool SupportEnabled { get; init; } = false;

    /// <summary>
    /// Part height along Z-axis in millimeters.
    /// </summary>
    public decimal HeightMm { get; init; }

    /// <summary>
    /// For Scanning technology only.
    /// </summary>
    public string? ScanningTier { get; init; }

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
    /// Additional notes about the pricing.
    /// </summary>
    public string? Notes { get; init; }

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
    /// Date until which this price is valid.
    /// </summary>
    public required DateTime ValidUntil { get; init; }

    /// <summary>
    /// Calculation duration.
    /// </summary>
    public required TimeSpan CalculationDuration { get; init; }
}
