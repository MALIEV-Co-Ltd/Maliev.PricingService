using System.Net;
using System.Net.Http.Json;
using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.PricingService.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Maliev.PricingService.Infrastructure.Clients;

public class MaterialServiceClient : IMaterialServiceClient
{
    private const int MaterialPageSize = 100;
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MaterialServiceClient> _logger;

    public MaterialServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<MaterialServiceClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MaterialDto>> GetMaterialsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var materials = new List<MaterialDto>();
            var page = 1;
            var totalPages = 1;

            while (page <= totalPages)
            {
                var response = await _httpClient.GetFromJsonAsync<PagedMaterialResponse<MaterialDto>>(
                    $"/material/v1/materials?page={page}&pageSize={MaterialPageSize}",
                    cancellationToken);

                if (response is null)
                    break;

                materials.AddRange(response.Items);
                totalPages = Math.Max(response.TotalPages, page);
                page++;
            }

            return materials;
        }
        catch (Exception ex) when (IsAuthorizationFailure(ex))
        {
            _logger.LogWarning(ex, "MaterialService authorization failed while fetching the material catalog");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching material catalog");
            return [];
        }
    }

    public async Task<MaterialDto?> GetMaterialAsync(Guid materialId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<MaterialDto>($"/material/v1/materials/{materialId}", cancellationToken);
        }
        catch (Exception ex) when (IsAuthorizationFailure(ex))
        {
            _logger.LogWarning(ex, "MaterialService authorization failed while fetching material {MaterialId}", materialId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching material {MaterialId}", materialId);
            return null;
        }
    }

    public async Task<ManufacturingProcessDto?> GetProcessAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ManufacturingProcessDto>($"/material/v1/reference/processes/{processId}", cancellationToken);
        }
        catch (Exception ex) when (IsAuthorizationFailure(ex))
        {
            _logger.LogWarning(ex, "MaterialService authorization failed while fetching process {ProcessId}", processId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching process {ProcessId}", processId);
            return null;
        }
    }

    public async Task<MaterialDto?> GetDefaultMaterialAsync(string processType, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<MaterialDto>($"/material/v1/materials/default/{processType}", cancellationToken);
        }
        catch (Exception ex) when (IsAuthorizationFailure(ex))
        {
            _logger.LogWarning(ex, "MaterialService authorization failed while fetching the default material for {ProcessType}", processType);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching default material for {ProcessType}", processType);
            return null;
        }
    }

    private static bool IsAuthorizationFailure(Exception exception) =>
        exception is ServiceTokenExchangeException or
            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden };

    private sealed record PagedMaterialResponse<T>
    {
        public IReadOnlyList<T> Items { get; init; } = [];

        public int TotalPages { get; init; }
    }
}
