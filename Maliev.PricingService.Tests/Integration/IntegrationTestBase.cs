using System.Net.Http.Json;
using System.Text.Json;
using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Domain.Entities;
using Maliev.PricingService.Infrastructure.Persistence;
using Maliev.PricingService.Tests.TestFixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Maliev.PricingService.Tests.Integration;

public abstract class IntegrationTestBase
{
    protected readonly PricingServiceTestFactory Factory;
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected IntegrationTestBase(PricingServiceTestFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected async Task<T?> GetResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }
}
