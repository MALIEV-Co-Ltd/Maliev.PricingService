using System.Net.Http.Json;

using Maliev.PricingService.Application.Interfaces;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

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

        var normalizedFromCurrency = fromCurrency.ToUpperInvariant();
        var normalizedToCurrency = toCurrency.ToUpperInvariant();
        var cacheKey = $"fx:{normalizedFromCurrency}:{normalizedToCurrency}";
        if (_cache.TryGetValue(cacheKey, out decimal cachedRate))
            return cachedRate;

        try
        {
            var response = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>(
                $"/currency/v1/rates?from={normalizedFromCurrency}&to={normalizedToCurrency}",
                cancellationToken);

            if (response is null || response.Rate <= 0m)
            {
                throw new InvalidOperationException(
                    $"CurrencyService returned an invalid exchange rate for {normalizedFromCurrency}/{normalizedToCurrency}.");
            }

            _cache.Set(cacheKey, response.Rate, TimeSpan.FromSeconds(60));
            return response.Rate;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Failed to fetch exchange rate {From} to {To}; pricing calculation cannot continue",
                normalizedFromCurrency,
                normalizedToCurrency);
            throw;
        }
    }

    private record ExchangeRateResponse(decimal Rate);
}
