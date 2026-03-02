namespace Maliev.PricingService.Domain.Enums;

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
