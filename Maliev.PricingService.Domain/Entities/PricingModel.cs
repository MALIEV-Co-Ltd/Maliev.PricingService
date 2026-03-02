using System.ComponentModel.DataAnnotations;

namespace Maliev.PricingService.Domain.Entities;

/// <summary>
/// Tracks ML model training and deployment for pricing predictions.
/// </summary>
public class PricingModel
{
    /// <summary>
    /// Unique identifier for the model.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable name for the model.
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Version string (e.g., "1.0.0", "2.1.0-beta").
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Type of pricing model.
    /// </summary>
    [Required]
    public PricingModelType ModelType { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Training Metadata
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Number of training samples used.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int TrainingDataCount { get; set; }

    /// <summary>
    /// When training started.
    /// </summary>
    [Required]
    public DateTime TrainingStartedAt { get; set; }

    /// <summary>
    /// When training completed.
    /// </summary>
    [Required]
    public DateTime TrainingCompletedAt { get; set; }

    /// <summary>
    /// Total training duration.
    /// </summary>
    [Required]
    public TimeSpan TrainingDuration { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Model Performance Metrics
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Mean Absolute Error of predictions.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal MeanAbsoluteError { get; set; }

    /// <summary>
    /// Mean Absolute Percentage Error of predictions.
    /// </summary>
    [Range(0, 100)]
    public decimal MeanAbsolutePercentageError { get; set; }

    /// <summary>
    /// R-squared (coefficient of determination).
    /// </summary>
    [Range(0, 1)]
    public decimal RSquared { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Deployment Status
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether this model is currently active for predictions.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When the model was deployed to production.
    /// </summary>
    public DateTime? DeployedAt { get; set; }

    /// <summary>
    /// When the model was retired from production.
    /// </summary>
    public DateTime? RetiredAt { get; set; }

    // ─────────────────────────────────────────────────────────────
    // Storage
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Path to the ONNX model file in storage.
    /// </summary>
    [Required]
    [StringLength(500)]
    public string ModelFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Optimistic concurrency token.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// Type of ML model for pricing.
/// </summary>
public enum PricingModelType
{
    /// <summary>
    /// Optimizes price for conversion and profit.
    /// </summary>
    PriceOptimization = 1,

    /// <summary>
    /// Estimates actual production cost.
    /// </summary>
    CostEstimation = 2,

    /// <summary>
    /// Predicts manufacturing success rate.
    /// </summary>
    SuccessPrediction = 3
}
