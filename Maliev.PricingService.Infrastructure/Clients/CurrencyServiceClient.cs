using Maliev.PricingService.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Maliev.PricingService.Infrastructure.Clients;

public class CurrencyServiceClient : ICurrencyServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CurrencyServiceClient> _logger;
    private readonly IMemoryCache _cache;

    public CurrencyServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CurrencyServiceClient> logger,
        IMemoryCache cache)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _cache = cache;
    }

    public async Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        var rate = await GetExchangeRateAsync(fromCurrency, toCurrency, cancellationToken);
        return amount * rate;
    }

    public async Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return 1.0m;

        var cacheKey = $"fx:{fromCurrency.ToUpperInvariant()}:{toCurrency.ToUpperInvariant()}";
        if (_cache.TryGetValue(cacheKey, out decimal cachedRate))
            return cachedRate;

        try
        {
            var response = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>(
                $"/v1/currency/rate?from={fromCurrency}&to={toCurrency}", cancellationToken);
            var rate = response?.Rate ?? 1.0m;
            _cache.Set(cacheKey, rate, TimeSpan.FromSeconds(60));
            return rate;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch exchange rate {From}→{To}; falling back to 1.0 (THB)", fromCurrency, toCurrency);
            return 1.0m;
        }
    }

    private record ConversionResponse(decimal Result);
    private record ExchangeRateResponse(decimal Rate);
}
