using System.Net;
using System.Text;
using Maliev.PricingService.Infrastructure.Clients;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maliev.PricingService.Tests.Unit;

public class MaterialServiceClientTests
{
    [Fact]
    public async Task GetMaterialsAsync_UsesPagedMaterialServiceRoute()
    {
        var materialId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var handler = new CapturingHandler(
            $$"""
            {
              "items": [
                {
                  "id": "{{materialId}}",
                  "code": "AL6061",
                  "name": "Aluminum 6061",
                  "densityGramPerCm3": 2.7,
                  "pricePerKg": 120
                }
              ],
              "totalCount": 1,
              "page": 1,
              "pageSize": 100,
              "totalPages": 1
            }
            """);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://MaterialService")
        };
        var sut = new MaterialServiceClient(
            httpClient,
            new HttpContextAccessor(),
            NullLogger<MaterialServiceClient>.Instance);

        var materials = await sut.GetMaterialsAsync();

        var material = Assert.Single(materials);
        Assert.Equal("/material/v1/materials?page=1&pageSize=100", handler.RequestUri?.PathAndQuery);
        Assert.Equal("AL6061", material.Code);
        Assert.Equal(2.7m, material.DensityGramPerCm3);
    }

    [Fact]
    public async Task GetMaterialAsync_ValidMaterialId_UsesMaterialServiceRoute()
    {
        var materialId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var handler = new CapturingHandler(
            $$"""
            {
              "id": "{{materialId}}",
              "code": "AL6061",
              "name": "Aluminum 6061",
              "densityGramPerCm3": 2.7,
              "pricePerKg": 120
            }
            """);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://MaterialService")
        };
        var sut = new MaterialServiceClient(
            httpClient,
            new HttpContextAccessor(),
            NullLogger<MaterialServiceClient>.Instance);

        var material = await sut.GetMaterialAsync(materialId);

        Assert.NotNull(material);
        Assert.Equal("/material/v1/materials/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", handler.RequestUri?.PathAndQuery);
        Assert.Equal("AL6061", material.Code);
        Assert.Equal(2.7m, material.DensityGramPerCm3);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _content;

        public CapturingHandler(string content)
        {
            _content = content;
        }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
