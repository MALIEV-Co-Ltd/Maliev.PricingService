using System.Net;
using System.Text;
using Maliev.PricingService.Infrastructure.Clients;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maliev.PricingService.Tests.Unit;

public class CurrencyServiceClientTests
{
    [Fact]
    public async Task GetExchangeRateAsync_DifferentCurrencies_UsesCurrencyServiceRatesContract()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            """
            {
              "fromCurrency": "THB",
              "toCurrency": "USD",
              "rate": 0.0275,
              "timestamp": "2026-07-16T00:00:00Z",
              "source": "test",
              "isTransitive": false,
              "mode": "live"
            }
            """);
        var client = CreateClient(handler);

        var rate = await client.GetExchangeRateAsync("thb", "usd");

        Assert.Equal(0.0275m, rate);
        Assert.Equal("/currency/v1/rates?from=THB&to=USD", handler.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetExchangeRateAsync_DownstreamUnavailable_DoesNotSubstituteParityRate()
    {
        var handler = new StubHandler(HttpStatusCode.ServiceUnavailable, "{}");
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetExchangeRateAsync("THB", "USD"));
    }

    [Fact]
    public async Task GetExchangeRateAsync_InvalidRate_DoesNotCacheOrReturnIt()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"rate\":0}");
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetExchangeRateAsync("THB", "USD"));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetExchangeRateAsync("THB", "USD"));

        Assert.Equal(2, handler.RequestCount);
    }

    private static CurrencyServiceClient CreateClient(StubHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://CurrencyService")
        };

        return new CurrencyServiceClient(
            httpClient,
            new HttpContextAccessor(),
            NullLogger<CurrencyServiceClient>.Instance,
            new MemoryCache(new MemoryCacheOptions()));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public StubHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        public Uri? RequestUri { get; private set; }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestCount++;

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            });
        }
    }
}
