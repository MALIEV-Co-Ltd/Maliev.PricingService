using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Maliev.PricingService.Api.Clients;

/// <summary>
/// HTTP client for CurrencyService.
/// </summary>
public class CurrencyServiceClient : ICurrencyServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CurrencyServiceClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyServiceClient"/> class.
    /// </summary>
    public CurrencyServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CurrencyServiceClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<decimal> ConvertAsync(
        decimal amount,
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        if (fromCurrency == toCurrency)
        {
            return amount;
        }

        var rate = await GetExchangeRateAsync(fromCurrency, toCurrency, cancellationToken);
        return amount * rate;
    }

    /// <inheritdoc/>
    public async Task<decimal> GetExchangeRateAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        if (fromCurrency == toCurrency)
        {
            return 1m;
        }

        ForwardAuthorizationHeader();

        try
        {
            var response = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>(
                $"/currency/v1/rates?from={fromCurrency}&to={toCurrency}",
                cancellationToken);

            return response?.Rate ?? 1m;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to get exchange rate from {From} to {To}", fromCurrency, toCurrency);
            return 1m; // Default to 1:1 if service unavailable
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

    private record ExchangeRateResponse(decimal Rate);
}
