namespace Maliev.PricingService.Api.Models.Snapshots;

/// <summary>
/// Response model for a pricing snapshot.
/// </summary>
public class PricingSnapshotResponse
{
    /// <summary>
    /// Unique identifier for the snapshot.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The ID of the order associated with this pricing.
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the quotation associated with this pricing.
    /// </summary>
    public Guid? QuotationId { get; set; }

    /// <summary>
    /// The ID of the employee who performed the pricing.
    /// </summary>
    public string EmployeeId { get; set; } = string.Empty;

    /// <summary>
    /// The manufacturing technology used.
    /// </summary>
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
    /// The infill percentage.
    /// </summary>
    public decimal? InfillPercentage { get; set; }

    /// <summary>
    /// The type of support structure used.
    /// </summary>
    public string? SupportType { get; set; }

    /// <summary>
    /// The print orientation.
    /// </summary>
    public string? PrintOrientation { get; set; }

    /// <summary>
    /// The calculated price.
    /// </summary>
    public decimal CalculatedPrice { get; set; }

    /// <summary>
    /// The manual override price.
    /// </summary>
    public decimal? ManualOverridePrice { get; set; }

    /// <summary>
    /// The ID of the detailed audit record.
    /// </summary>
    public Guid PricingAuditRecordId { get; set; }

    /// <summary>
    /// The status of this snapshot.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the snapshot that superseded this one.
    /// </summary>
    public Guid? SupersededById { get; set; }

    /// <summary>
    /// Timestamp when this snapshot was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this snapshot was accepted.
    /// </summary>
    public DateTime? AcceptedAt { get; set; }
}
