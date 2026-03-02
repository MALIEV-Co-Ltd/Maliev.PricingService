using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maliev.PricingService.Application.Services;

public class RuleBasedPricingEngine : IPricingEngine
{
    private readonly ILogger<RuleBasedPricingEngine> _logger;

    public RuleBasedPricingEngine(ILogger<RuleBasedPricingEngine> logger)
    {
        _logger = logger;
    }

    public async Task<PricingResult> CalculateAsync(PricingRequest request, PricingConfiguration configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating rule-based price for {MaterialCode}", request.MaterialCode);
        
        return new PricingResult
        {
            UnitPrice = 110.0m,
            TotalAmount = 110.0m * request.Quantity,
            EngineName = "Rule-v1"
        };
    }
}
