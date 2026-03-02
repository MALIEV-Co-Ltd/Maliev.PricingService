using Maliev.PricingService.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Maliev.PricingService.Infrastructure.Clients;

public class CurrencyServiceClient : ICurrencyServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CurrencyServiceClient> _logger;

    public CurrencyServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<CurrencyServiceClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<decimal> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ConversionResponse>($"/v1/currency/convert?amount={amount}&from={fromCurrency}&to={toCurrency}", cancellationToken);
            return response?.Result ?? amount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting currency from {From} to {To}", fromCurrency, toCurrency);
            return amount;
        }
    }

    public async Task<decimal> GetExchangeRateAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<ExchangeRateResponse>($"/v1/currency/rate?from={fromCurrency}&to={toCurrency}", cancellationToken);
            return response?.Rate ?? 1.0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching exchange rate from {From} to {To}", fromCurrency, toCurrency);
            return 1.0m;
        }
    }

    private record ConversionResponse(decimal Result);
    private record ExchangeRateResponse(decimal Rate);
}
