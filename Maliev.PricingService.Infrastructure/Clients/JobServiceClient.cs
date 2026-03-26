using Maliev.PricingService.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Maliev.PricingService.Infrastructure.Clients;

/// <summary>
/// HTTP client for the Job Service to fetch queue depth information.
/// </summary>
public class JobServiceClient : IJobServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JobServiceClient> _logger;

    public JobServiceClient(HttpClient httpClient, ILogger<JobServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Dictionary<string, int>> GetQueueDepthByTechnologyAsync(string? technology, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(technology)
                ? "/job/v1/jobs/queue-depth"
                : $"/job/v1/jobs/queue-depth?technology={Uri.EscapeDataString(technology)}";

            var result = await _httpClient.GetFromJsonAsync<Dictionary<string, int>>(url, cancellationToken);
            return result ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get queue depth from JobService for technology {Technology}. Returning 0.", technology);
            return [];
        }
    }
}
