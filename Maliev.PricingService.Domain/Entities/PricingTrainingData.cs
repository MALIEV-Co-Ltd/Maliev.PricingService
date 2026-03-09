using System.ComponentModel.DataAnnotations;

namespace Maliev.PricingService.Domain.Entities;

/// <summary>
/// Training data collected from completed jobs for ML model improvement.
/// Links pricing audit records to actual job outcomes.
/// </summary>
public class PricingTrainingData
{
    /// <summary>
    /// Unique identifier for the training data record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the pricing audit record.
    /// </summary>
    [Required]
    public Guid PricingAuditRecordId { get; set; }

    /// <summary>
    /// Reference to the order created from this quote.
    /// </summary>
    public Guid? OrderId { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Outcome Tracking
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether the customer accepted the quote.
    /// </summary>
    public bool CustomerAccepted { get; set; }

    /// <summary>
    /// When the customer accepted the quote.
    /// </summary>
    public DateTime? AcceptedAt { get; set; }

    /// <summary>
    /// Whether the job was completed.
    /// </summary>
    public bool JobCompleted { get; set; }

    /// <summary>
    /// When the job was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Whether the manufacturing succeeded (no reprints needed).
    /// </summary>
    public bool JobSucceeded { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Actual Costs (filled after job completion)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Actual material used in cubic centimeters.
    /// </summary>
    [Range(0, 1000000)]
    public decimal? ActualMaterialUsedCm3 { get; set; }

    /// <summary>
    /// Actual print time in hours.
    /// </summary>
    [Range(0, 10000)]
    public decimal? ActualPrintTimeHours { get; set; }

    /// <summary>
    /// Actual labor hours spent.
    /// </summary>
    [Range(0, 10000)]
    public decimal? ActualLaborHours { get; set; }

    /// <summary>
    /// Actual total cost of the job.
    /// </summary>
    [Range(0, 100000000)]
    public decimal? ActualTotalCost { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Profit Analysis
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Actual profit margin: (QuotedPrice - ActualCost) / QuotedPrice.
    /// </summary>
    [Range(-10, 1)]
    public decimal? ActualProfitMargin { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Training Status
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether this record has been used for model training.
    /// </summary>
    public bool UsedForTraining { get; set; }

    /// <summary>
    /// Reference to the model trained using this data.
    /// </summary>
    public Guid? TrainedModelId { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Navigation Properties
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigation to the pricing audit record.
    /// </summary>
    public PricingAuditRecord? PricingAuditRecord { get; set; }

    /// <summary>
    /// Navigation to the model trained.
    /// </summary>
    public PricingModel? TrainedModel { get; set; }
}
