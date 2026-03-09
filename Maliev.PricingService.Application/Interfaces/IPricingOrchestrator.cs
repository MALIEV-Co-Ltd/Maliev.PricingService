using Maliev.PricingService.Application.DTOs;

namespace Maliev.PricingService.Application.Interfaces;

public interface IPricingOrchestrator
{
    Task<PricingResult> CalculatePriceAsync(PricingRequest request, CancellationToken cancellationToken = default);
    Task<PricingResult> AuditCalculationAsync(Guid calculationId, PricingResult result, CancellationToken cancellationToken = default);
}
