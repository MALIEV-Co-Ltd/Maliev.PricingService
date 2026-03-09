using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Domain.Entities;

namespace Maliev.PricingService.Application.Interfaces;

/// <summary>
/// Interface for pricing calculation engines.
/// </summary>
public interface IPricingEngine
{
    /// <summary>
    /// Calculates the price for a given set of inputs.
    /// </summary>
    Task<PricingResult> CalculateAsync(
        PricingRequest request,
        PricingConfiguration configuration,
        CancellationToken cancellationToken = default);
}
