using System.ComponentModel.DataAnnotations;

namespace Maliev.PricingService.Data.Entities;

/// <summary>
/// Immutable audit record of every pricing calculation.
/// Never updated after creation - provides complete traceability.
/// </summary>
public class PricingAuditRecord
{
    /// <summary>
    /// Unique identifier for the audit record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the quotation created from this pricing (set after quotation creation).
    /// </summary>
    public Guid? QuotationId { get; set; }

    /// <summary>
    /// Reference to the analyzed file.
    /// </summary>
    [Required]
    public Guid FileId { get; set; }

    /// <summary>
    /// Reference to the customer requesting the quote.
    /// </summary>
    [Required]
    public Guid CustomerId { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Input: Geometry Metrics (snapshot at time of calculation)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Part volume in cubic centimeters.
    /// </summary>
    [Range(0.001, 1000000)]
    public decimal InputVolumeCm3 { get; set; }

    /// <summary>
    /// Estimated support volume in cubic centimeters.
    /// </summary>
    [Range(0, 1000000)]
    public decimal InputSupportVolumeCm3 { get; set; }

    /// <summary>
    /// Surface area in square centimeters.
    /// </summary>
    [Range(0.01, 10000000)]
    public decimal InputSurfaceAreaCm2 { get; set; }

    /// <summary>
    /// Bounding box X dimension in millimeters.
    /// </summary>
    [Range(0.1, 10000)]
    public decimal InputBoundingBoxX { get; set; }

    /// <summary>
    /// Bounding box Y dimension in millimeters.
    /// </summary>
    [Range(0.1, 10000)]
    public decimal InputBoundingBoxY { get; set; }

    /// <summary>
    /// Bounding box Z dimension in millimeters.
    /// </summary>
    [Range(0.1, 10000)]
    public decimal InputBoundingBoxZ { get; set; }

    /// <summary>
    /// Whether the mesh is watertight/manifold.
    /// </summary>
    public bool InputIsManifold { get; set; }

    /// <summary>
    /// Number of triangles in the mesh.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int InputTriangleCount { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Input: Material and Process
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Reference to the material in MaterialService.
    /// </summary>
    [Required]
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Material code at time of calculation (denormalized for audit).
    /// </summary>
    [Required]
    [StringLength(50)]
    public string MaterialCode { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the manufacturing process in MaterialService.
    /// </summary>
    [Required]
    public Guid ManufacturingProcessId { get; set; }

    /// <summary>
    /// Manufacturing process name at time of calculation (denormalized for audit).
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ManufacturingProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Quantity of parts requested.
    /// </summary>
    [Range(1, 1000000)]
    public int Quantity { get; set; } = 1;

    // ─────────────────────────────────────────────────────────────
    // Pricing Configuration Snapshot
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Reference to the pricing configuration used.
    /// </summary>
    [Required]
    public Guid PricingConfigurationId { get; set; }

    /// <summary>
    /// Material price per cm3 at time of calculation.
    /// </summary>
    public decimal ConfigMaterialPricePerCm3 { get; set; }

    /// <summary>
    /// Support material price per cm3 at time of calculation.
    /// </summary>
    public decimal ConfigSupportPricePerCm3 { get; set; }

    /// <summary>
    /// Machine hourly rate at time of calculation.
    /// </summary>
    public decimal ConfigMachineHourlyRate { get; set; }

    /// <summary>
    /// Margin multiplier at time of calculation.
    /// </summary>
    public decimal ConfigMarginMultiplier { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Strategy Used
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Pricing strategy used for this calculation.
    /// </summary>
    [Required]
    public PricingStrategy Strategy { get; set; }

    /// <summary>
    /// ML model version if ML strategy was used.
    /// </summary>
    [StringLength(50)]
    public string? MLModelVersion { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Output: Price Breakdown
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Cost of material for the part.
    /// </summary>
    [Range(0, 10000000)]
    public decimal MaterialCost { get; set; }

    /// <summary>
    /// Cost of support material.
    /// </summary>
    [Range(0, 10000000)]
    public decimal SupportMaterialCost { get; set; }

    /// <summary>
    /// Cost of machine time.
    /// </summary>
    [Range(0, 10000000)]
    public decimal MachineTimeCost { get; set; }

    /// <summary>
    /// One-time setup cost.
    /// </summary>
    [Range(0, 10000000)]
    public decimal SetupCost { get; set; }

    /// <summary>
    /// Surcharge for complex geometry.
    /// </summary>
    [Range(0, 10000000)]
    public decimal ComplexitySurcharge { get; set; }

    /// <summary>
    /// Subtotal before margin is applied.
    /// </summary>
    [Range(0, 100000000)]
    public decimal SubtotalBeforeMargin { get; set; }

    /// <summary>
    /// Margin amount added.
    /// </summary>
    [Range(0, 100000000)]
    public decimal MarginAmount { get; set; }

    /// <summary>
    /// Final price per unit.
    /// </summary>
    [Range(0, 100000000)]
    public decimal TotalUnitPrice { get; set; }

    /// <summary>
    /// Total price (TotalUnitPrice × Quantity).
    /// </summary>
    [Range(0, 1000000000)]
    public decimal TotalPrice { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Confidence and Validity
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Confidence level of the price estimate (0.0 - 1.0).
    /// </summary>
    [Range(0, 1)]
    public decimal ConfidenceLevel { get; set; }

    /// <summary>
    /// Currency code for the price.
    /// </summary>
    [Required]
    [StringLength(3)]
    public string CurrencyCode { get; set; } = "THB";

    /// <summary>
    /// Date from which this price is valid.
    /// </summary>
    [Required]
    public DateTime ValidFrom { get; set; }

    /// <summary>
    /// Date until which this price is valid.
    /// </summary>
    [Required]
    public DateTime ValidUntil { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Audit Metadata
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Timestamp when the calculation was performed.
    /// </summary>
    [Required]
    public DateTime CalculatedAt { get; set; }

    /// <summary>
    /// System that performed the calculation.
    /// </summary>
    [Required]
    [StringLength(50)]
    public string CalculatedBySystem { get; set; } = "PricingService";

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// </summary>
    [StringLength(100)]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Time taken to perform the calculation.
    /// </summary>
    [Required]
    public TimeSpan CalculationDuration { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Navigation Properties
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigation to the pricing configuration used.
    /// </summary>
    public PricingConfiguration? PricingConfiguration { get; set; }

    /// <summary>
    /// Navigation to training data derived from this record.
    /// </summary>
    public PricingTrainingData? TrainingData { get; set; }
}

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
