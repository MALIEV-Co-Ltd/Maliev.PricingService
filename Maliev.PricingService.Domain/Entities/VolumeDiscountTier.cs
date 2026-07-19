namespace Maliev.PricingService.Domain.Entities;

/// <summary>
/// Represents a volume discount tier applied to bulk orders.
/// </summary>
public class VolumeDiscountTier
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Minimum quantity for this tier (inclusive).</summary>
    public int MinQuantity { get; set; }

    /// <summary>Maximum quantity for this tier (inclusive). Null means no upper limit.</summary>
    public int? MaxQuantity { get; set; }

    /// <summary>Discount percentage applied to the unit price (e.g. 10.0 = 10% discount).</summary>
    public decimal DiscountPercent { get; set; }

    /// <summary>Whether this tier is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Display ordering.</summary>
    public int SortOrder { get; set; }

    /// <summary>Timestamp when created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Timestamp when last updated.</summary>
    public DateTime? UpdatedAt { get; set; }
}
