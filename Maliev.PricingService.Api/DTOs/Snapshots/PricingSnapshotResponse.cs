using Maliev.PricingService.Domain.Entities;
namespace Maliev.PricingService.Application.DTOs.Snapshots;

/// <summary>
/// Response model for a pricing snapshot.
/// </summary>
public class PricingSnapshotResponse
{
    /// <summary>
    /// Unique identifier for the snapshot.
    /// </summary>
    public Guid Id { get; set; }
    /// The ID of the order associated with this pricing.
    public string OrderId { get; set; } = string.Empty;
    /// The ID of the quotation associated with this pricing.
    public Guid? QuotationId { get; set; }
    /// The ID of the employee who performed the pricing.
    public string EmployeeId { get; set; } = string.Empty;
    /// The manufacturing technology used.
    public string Technology { get; set; } = string.Empty;
    /// The material code used for pricing.
    public string? MaterialCode { get; set; }
    /// The material brand used for pricing.
    public string? MaterialBrand { get; set; }
    /// The layer height in microns.
    public decimal? LayerHeight { get; set; }
    /// The infill percentage.
    public decimal? InfillPercentage { get; set; }
    /// The type of support structure used.
    public string? SupportType { get; set; }
    /// The print orientation.
    public string? PrintOrientation { get; set; }
    /// The calculated price.
    public decimal CalculatedPrice { get; set; }
    /// The manual override price.
    public decimal? ManualOverridePrice { get; set; }
    /// The ID of the detailed audit record.
    public Guid PricingAuditRecordId { get; set; }
    /// The status of this snapshot.
    public string Status { get; set; } = string.Empty;
    /// The ID of the snapshot that superseded this one.
    public Guid? SupersededById { get; set; }
    /// Timestamp when this snapshot was created.
    public DateTime CreatedAt { get; set; }
    /// Timestamp when this snapshot was accepted.
    public DateTime? AcceptedAt { get; set; }
}
