namespace Maliev.PricingService.Api.Interfaces;

using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Data.Entities;

/// <summary>
/// Orchestrates the pricing calculation process.
/// </summary>
public interface IPricingOrchestrator
{
    /// <summary>
    /// Calculates the price for a given set of inputs.
    /// </summary>
    /// <param name="request">The pricing request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A pricing result.</returns>
    Task<PricingResult> CalculatePriceAsync(PricingRequest request, CancellationToken cancellationToken = default);
}
