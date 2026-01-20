namespace Maliev.PricingService.Api.Services;

using Maliev.PricingService.Data.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implementation of ML-enhanced pricing engine.
/// </summary>
public class MLPricingEngine : IMLPricingEngine
{
    private readonly ILogger<MLPricingEngine> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MLPricingEngine"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public MLPricingEngine(ILogger<MLPricingEngine> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<PricingResult> CalculateAsync(PricingRequest request, PricingConfiguration config, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("ML Pricing Engine not yet implemented. Using fallback.");
        throw new NotImplementedException("ML Pricing Engine is under construction.");
    }
}
