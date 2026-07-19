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
    /// Manual pricing by staff.
    /// </summary>
    Manual = 3
}
