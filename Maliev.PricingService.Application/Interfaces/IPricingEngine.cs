using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Domain.Entities;

namespace Maliev.PricingService.Application.Interfaces;

public interface IPricingEngine
{
    Task<EngineResult> CalculateAsync(
        PricingRequest request,
        PricingConfiguration configuration,
        CancellationToken cancellationToken = default);
}
