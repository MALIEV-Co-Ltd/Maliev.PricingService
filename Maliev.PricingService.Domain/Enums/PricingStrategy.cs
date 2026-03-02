namespace Maliev.PricingService.Domain.Enums;

/// <summary>
/// Pricing strategy used for calculations.
/// </summary>
public enum PricingStrategy
{
    /// <summary>
    /// Deterministic rule-based calculation.
    /// </summary>
    RuleBased = 1,

    /// <summary>
    /// Machine learning enhanced calculation.
    /// </summary>
    MLEnhanced = 2,

    /// <summary>
    /// Manual pricing by staff.
    /// </summary>
    Manual = 3,

    /// <summary>
    /// Combination of rule-based with ML adjustments.
    /// </summary>
    Hybrid = 4
}
