using Maliev.PricingService.Data.Entities;
using Maliev.PricingService.Api.Services;

namespace Maliev.PricingService.Api.Interfaces;

/// <summary>
/// Interface for pricing calculation engines.
/// </summary>
public interface IPricingEngine
{
    /// <summary>
    /// Calculates the price based on the input dimensions and configuration.
    /// </summary>
    /// <param name="request">The pricing request containing geometry metrics and quantity.</param>
    /// <param name="config">The pricing configuration to use.</param>
    /// <returns>The result of the pricing calculation.</returns>
    PricingResult CalculatePrice(PricingRequest request, PricingConfiguration config);
}
