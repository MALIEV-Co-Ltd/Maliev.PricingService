using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Maliev.PricingService.Data.Entities;

/// <summary>
/// Captures a snapshot of the manufacturing configuration used to price an order.
/// Provides traceability for why a specific price was calculated.
/// </summary>
public class PricingSnapshot
{
    /// <summary>
    /// Unique identifier for the snapshot.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The ID of the order associated with this pricing.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the quotation associated with this pricing (optional).
    /// </summary>
    public Guid? QuotationId { get; set; }

    /// <summary>
    /// The ID of the employee who performed the pricing.
    /// </summary>
    [Required]
    public string EmployeeId { get; set; } = string.Empty;

    // Manufacturing Configuration

    /// <summary>
    /// The manufacturing technology used (e.g., FDM, SLA, CNC).
    /// </summary>
    [MaxLength(50)]
    public string Technology { get; set; } = string.Empty; // FDM, SLA, etc.

    /// <summary>
    /// The material code used for pricing.
    /// </summary>
    [MaxLength(50)]
    public string? MaterialCode { get; set; }

    /// <summary>
    /// The material brand used for pricing.
    /// </summary>
    [MaxLength(100)]
    public string? MaterialBrand { get; set; }

    /// <summary>
    /// The layer height in microns.
    /// </summary>
    public decimal? LayerHeight { get; set; } // microns

    /// <summary>
    /// The infill percentage (0-100).
    /// </summary>
    public decimal? InfillPercentage { get; set; }

    /// <summary>
    /// The type of support structure used.
    /// </summary>
    [MaxLength(50)]
    public string? SupportType { get; set; }

    /// <summary>
    /// The print orientation (e.g., "XY", "Z").
    /// </summary>
    [MaxLength(50)]
    public string? PrintOrientation { get; set; }

    // Result

    /// <summary>
    /// The calculated price based on the pricing engine.
    /// </summary>
    public decimal CalculatedPrice { get; set; }

    /// <summary>
    /// An optional manual override price set by the employee.
    /// </summary>
    public decimal? ManualOverridePrice { get; set; }

    /// <summary>
    /// The ID of the detailed audit record for this calculation.
    /// </summary>
    [ForeignKey("PricingAuditRecord")]
    public Guid PricingAuditRecordId { get; set; }

    /// <summary>
    /// The detailed audit record for this calculation.
    /// </summary>
    public PricingAuditRecord PricingAuditRecord { get; set; } = null!;

    // Status

    /// <summary>
    /// The status of this snapshot (Draft, Accepted, Superseded).
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, Accepted, Superseded

    /// <summary>
    /// The ID of the snapshot that superseded this one.
    /// </summary>
    public Guid? SupersededById { get; set; }

    /// <summary>
    /// The snapshot that superseded this one.
    /// </summary>
    public PricingSnapshot? SupersededBy { get; set; }

    /// <summary>
    /// Timestamp when this snapshot was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this snapshot was accepted.
    /// </summary>
    public DateTime? AcceptedAt { get; set; }
}
