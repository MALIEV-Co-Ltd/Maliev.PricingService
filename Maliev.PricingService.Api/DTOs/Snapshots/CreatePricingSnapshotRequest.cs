using Maliev.PricingService.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace Maliev.PricingService.Application.DTOs.Snapshots;
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
    /// The ID of the quotation associated with this pricing (optional).
    public Guid? QuotationId { get; set; }
    /// The manufacturing technology used (e.g., FDM, SLA, CNC).
    public string Technology { get; set; } = string.Empty;
    /// The material code used for pricing.
    public string? MaterialCode { get; set; }
    /// The material brand used for pricing.
    public string? MaterialBrand { get; set; }
    /// The layer height in microns.
    public decimal? LayerHeight { get; set; }
    /// The infill percentage (0-100).
    public decimal? InfillPercentage { get; set; }
    /// The type of support structure used.
    public string? SupportType { get; set; }
    /// The print orientation (e.g., "XY", "Z").
    public string? PrintOrientation { get; set; }
    /// The calculated price based on the pricing engine.
    public decimal CalculatedPrice { get; set; }
    /// An optional manual override price set by the employee.
    public decimal? ManualOverridePrice { get; set; }
    /// The ID of the detailed audit record for this calculation.
    public Guid PricingAuditRecordId { get; set; }
}
