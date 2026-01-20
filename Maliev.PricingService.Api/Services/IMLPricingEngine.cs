namespace Maliev.PricingService.Api.Services;

using Maliev.PricingService.Data.Entities;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Engine for ML-enhanced pricing calculations.
/// </summary>
public interface IMLPricingEngine
{
    /// <summary>
    /// Calculates price using ML models.
    /// </summary>
    /// <param name="request">The pricing request.</param>
    /// <param name="config">The pricing configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pricing result.</returns>
    Task<PricingResult> CalculateAsync(PricingRequest request, PricingConfiguration config, CancellationToken cancellationToken = default);
}
