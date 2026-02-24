namespace Maliev.PricingService.Api.Clients;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Maliev.PricingService.Api.DTOs;

/// <summary>HTTP client for calling ChatbotService vision analysis endpoints.</summary>
public interface IChatbotServiceClient
{
    /// <summary>Analyze design images to estimate CAD modeling complexity.</summary>
    Task<DesignAnalysisResult?> AnalyzeDesignAsync(
        List<string> imageUrls,
        string? description,
        bool requiresDrawings,
        CancellationToken cancellationToken = default);
}
