namespace Maliev.PricingService.Api.Services;

using Maliev.PricingService.Data.Entities;

/// <summary>
/// Interface for pricing calculation engines.
/// </summary>
public interface IPricingEngine
{
    /// <summary>
    /// Gets the pricing strategy implemented by this engine.
    /// </summary>
    PricingStrategy Strategy { get; }

    /// <summary>
    /// Calculates the price for a given set of inputs.
    /// </summary>
    /// <param name="request">The pricing request containing geometry and material information.</param>
    /// <param name="material">The material details from MaterialService.</param>
    /// <param name="rates">The machine rates from configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pricing result with full breakdown.</returns>
    Task<PricingResult> CalculateAsync(
        PricingRequest request,
        MaterialData material,
        MachineRates rates,
        CancellationToken cancellationToken = default);
}
