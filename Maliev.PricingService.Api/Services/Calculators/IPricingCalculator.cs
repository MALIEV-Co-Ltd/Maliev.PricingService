namespace Maliev.PricingService.Api.Services.Calculators;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for technology-specific pricing calculators.
/// </summary>
public interface IPricingCalculator
{
    /// <summary>
    /// Gets the manufacturing technology this calculator handles.
    /// </summary>
    ManufacturingTechnology Technology { get; }

    /// <summary>
    /// Calculates the price for a given request and material details.
    /// </summary>
    /// <param name="request">The pricing request.</param>
    /// <param name="material">The material properties.</param>
    /// <param name="rates">The machine rates and fees.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pricing result breakdown.</returns>
    Task<PricingResult> CalculateAsync(
        PricingRequest request,
        MaterialData material,
        MachineRates rates,
        CancellationToken cancellationToken = default);
}
