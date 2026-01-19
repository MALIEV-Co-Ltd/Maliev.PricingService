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

    /// <summary>
    /// Part volume in cubic centimeters.
    /// </summary>
    public decimal InputVolumeCm3 { get; set; }

    /// <summary>
    /// Estimated support volume in cubic centimeters.
    /// </summary>
    public decimal InputSupportVolumeCm3 { get; set; }

    /// <summary>
    /// Surface area in square centimeters.
    /// </summary>
    public decimal InputSurfaceAreaCm2 { get; set; }

    /// <summary>
    /// Final price per unit.
    /// </summary>
    public decimal TotalUnitPrice { get; set; }

    /// <summary>
    /// Total price (TotalUnitPrice * Quantity).
    /// </summary>
    public decimal TotalPrice { get; set; }

    /// <summary>
    /// Currency code for the price.
    /// </summary>
    [Required]
    [StringLength(3)]
    public string CurrencyCode { get; set; } = "THB";

    /// <summary>
    /// Timestamp when the calculation was performed.
    /// </summary>
    [Required]
    public DateTime CalculatedAt { get; set; }

    /// <summary>
    /// Pricing strategy used for this calculation.
    /// </summary>
    [Required]
    public string Strategy { get; set; } = "RuleBased";

    /// <summary>
    /// Navigation to the pricing configuration used.
    /// </summary>
    public Guid PricingConfigurationId { get; set; }
    /// <summary>
    /// Reference to the pricing configuration used.
    /// </summary>
    public PricingConfiguration? PricingConfiguration { get; set; }
}
