using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Domain.Entities;

namespace Maliev.PricingService.Application.Interfaces;

/// <summary>
/// Engine for ML-enhanced pricing calculations.
/// </summary>
public interface IMLPricingEngine
{
    /// <summary>
    /// Calculates price using ML models.
    /// </summary>
    Task<PricingResult> CalculateAsync(
        PricingRequest request,
        PricingConfiguration config,
        CancellationToken cancellationToken = default);
}
