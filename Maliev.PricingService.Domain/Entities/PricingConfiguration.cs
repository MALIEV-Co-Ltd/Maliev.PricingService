using System.ComponentModel.DataAnnotations;

namespace Maliev.PricingService.Domain.Entities;

/// <summary>
/// Configuration for pricing rules per material and manufacturing process combination.
/// Supports temporal versioning with effective dates.
/// </summary>
public class PricingConfiguration
{
    /// <summary>
    /// Unique identifier for the pricing configuration.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the material in MaterialService.
    /// </summary>
    [Required]
    public Guid MaterialId { get; set; }

    /// <summary>
    /// Reference to the manufacturing process in MaterialService.
    /// </summary>
    [Required]
    public Guid ManufacturingProcessId { get; set; }

    /// <summary>
    /// Material cost per cubic centimeter.
    /// </summary>
    [Range(0.0001, 100000)]
    public decimal MaterialPricePerCm3 { get; set; }

    /// <summary>
    /// Support material cost per cubic centimeter.
    /// </summary>
    [Range(0, 100000)]
    public decimal SupportMaterialPricePerCm3 { get; set; }

    /// <summary>
    /// Machine operating cost per hour.
    /// </summary>
    [Range(0, 100000)]
    public decimal MachineHourlyRate { get; set; }

    /// <summary>
    /// Printing speed in cubic centimeters per hour.
    /// </summary>
    [Range(0.1, 10000)]
    public decimal PrintSpeedCm3PerHour { get; set; }

    /// <summary>
    /// One-time setup cost per job.
    /// </summary>
    [Range(0, 100000)]
    public decimal SetupCostFlat { get; set; }

    /// <summary>
    /// Minimum order price regardless of calculations.
    /// </summary>
    [Range(0, 1000000)]
    public decimal MinimumOrderPrice { get; set; }

    /// <summary>
    /// Margin multiplier (e.g., 1.5 = 50% margin).
    /// </summary>
    [Range(1.0, 10.0)]
    public decimal MarginMultiplier { get; set; } = 1.5m;

    /// <summary>
    /// Surface area to volume ratio threshold for complexity surcharge.
    /// </summary>
    [Range(0.1, 100)]
    public decimal ComplexityThreshold { get; set; } = 2.0m;

    /// <summary>
    /// Percentage surcharge for complex parts.
    /// </summary>
    [Range(0, 100)]
    public decimal ComplexitySurchargePercent { get; set; } = 15m;

    /// <summary>
    /// Date from which this configuration is effective.
    /// </summary>
    [Required]
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// Date until which this configuration is effective. Null means no end date.
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>
    /// Whether this configuration is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp when the configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// User ID who created the configuration.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the configuration was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// User ID who last updated the configuration.
    /// </summary>
    [StringLength(100)]
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Optimistic concurrency token.
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public byte[] RowVersion { get; set; } = [];
}
