using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Maliev.PricingService.Api.DTOs;

namespace Maliev.PricingService.Api.Clients;

/// <summary>
/// HTTP client for ChatbotService.
/// </summary>
public class ChatbotServiceClient : IChatbotServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ChatbotServiceClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatbotServiceClient"/> class.
    /// </summary>
    public ChatbotServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ChatbotServiceClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<DesignAnalysisResult?> AnalyzeDesignAsync(
        List<string> imageUrls,
        string? description,
        bool requiresDrawings,
        CancellationToken cancellationToken = default)
    {
        ForwardAuthorizationHeader();

        try
        {
            var request = new AnalyzeDesignRequest
            {
                ImageUrls = imageUrls,
                Description = description,
                RequiresDrawings = requiresDrawings
            };

            var response = await _httpClient.PostAsJsonAsync(
                "/chatbot/v1/vision/analyze-design",
                request,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<DesignAnalysisResult>(
                    cancellationToken: cancellationToken);
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("ChatbotService AnalyzeDesign failed with status {StatusCode}: {ErrorBody}", 
                response.StatusCode, errorBody);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call ChatbotService for design analysis");
            return null;
        }
    }

    private void ForwardAuthorizationHeader()
    {
        var token = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);
        }
    }
}
