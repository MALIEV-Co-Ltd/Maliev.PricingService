namespace Maliev.PricingService.Domain.Entities;

/// <summary>
/// Represents a lead time option with associated price multiplier.
/// </summary>
public class LeadTimeOption
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Short code (e.g. "STANDARD", "EXPRESS").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name (e.g. "Standard", "Express").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Minimum business days for this lead time.</summary>
    public int MinBusinessDays { get; set; }

    /// <summary>Maximum business days for this lead time.</summary>
    public int MaxBusinessDays { get; set; }

    /// <summary>
    /// Price multiplier applied to the base price (e.g. 1.30 = 30% surcharge for express).
    /// </summary>
    public decimal PriceMultiplier { get; set; } = 1.0m;

    /// <summary>Whether this is the default lead time selection.</summary>
    public bool IsDefault { get; set; }

    /// <summary>Whether this option is active and shown to users.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Display ordering for UI lists.</summary>
    public int SortOrder { get; set; }

    /// <summary>Timestamp when created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Timestamp when last updated.</summary>
    public DateTime? UpdatedAt { get; set; }
}
