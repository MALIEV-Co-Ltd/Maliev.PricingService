using System.ComponentModel.DataAnnotations;

namespace Maliev.PricingService.Api.Models.Snapshots;

/// <summary>
/// Request to create a new pricing snapshot.
/// </summary>
public class CreatePricingSnapshotRequest
{
    /// <summary>
    /// The ID of the order associated with this pricing.
    /// </summary>
    [Required]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the quotation associated with this pricing (optional).
    /// </summary>
    public Guid? QuotationId { get; set; }

    /// <summary>
    /// The manufacturing technology used (e.g., FDM, SLA, CNC).
    /// </summary>
    [Required]
    public string Technology { get; set; } = string.Empty;

    /// <summary>
    /// The material code used for pricing.
    /// </summary>
    public string? MaterialCode { get; set; }

    /// <summary>
    /// The material brand used for pricing.
    /// </summary>
    public string? MaterialBrand { get; set; }

    /// <summary>
    /// The layer height in microns.
    /// </summary>
    public decimal? LayerHeight { get; set; }

    /// <summary>
    /// The infill percentage (0-100).
    /// </summary>
    public decimal? InfillPercentage { get; set; }

    /// <summary>
    /// The type of support structure used.
    /// </summary>
    public string? SupportType { get; set; }

    /// <summary>
    /// The print orientation (e.g., "XY", "Z").
    /// </summary>
    public string? PrintOrientation { get; set; }

    /// <summary>
    /// The calculated price based on the pricing engine.
    /// </summary>
    [Required]
    public decimal CalculatedPrice { get; set; }

    /// <summary>
    /// An optional manual override price set by the employee.
    /// </summary>
    public decimal? ManualOverridePrice { get; set; }

    /// <summary>
    /// The ID of the detailed audit record for this calculation.
    /// </summary>
    [Required]
    public Guid PricingAuditRecordId { get; set; }
}
