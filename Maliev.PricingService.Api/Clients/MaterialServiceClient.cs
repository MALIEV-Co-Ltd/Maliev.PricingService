using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Maliev.PricingService.Api.Clients;

/// <summary>
/// HTTP client for MaterialService.
/// </summary>
public class MaterialServiceClient : IMaterialServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MaterialServiceClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MaterialServiceClient"/> class.
    /// </summary>
    public MaterialServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<MaterialServiceClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MaterialDto?> GetMaterialAsync(Guid materialId, CancellationToken cancellationToken = default)
    {
        ForwardAuthorizationHeader();

        try
        {
            return await _httpClient.GetFromJsonAsync<MaterialDto>(
                $"/material/v1/materials/{materialId}",
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to get material {MaterialId}", materialId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<ManufacturingProcessDto?> GetProcessAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        ForwardAuthorizationHeader();

        try
        {
            return await _httpClient.GetFromJsonAsync<ManufacturingProcessDto>(
                $"/material/v1/processes/{processId}",
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to get process {ProcessId}", processId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<MaterialDto?> GetDefaultMaterialAsync(string processType, CancellationToken cancellationToken = default)
    {
        ForwardAuthorizationHeader();

        try
        {
            return await _httpClient.GetFromJsonAsync<MaterialDto>(
                $"/material/v1/materials/default?processType={processType}",
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to get default material for process type {ProcessType}", processType);
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
