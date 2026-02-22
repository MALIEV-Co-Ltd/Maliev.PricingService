using System.Net.Http.Json;
using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Api.Clients;
using Maliev.PricingService.Data.Entities;
using Maliev.PricingService.Tests.TestFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.TestHost;
using Maliev.PricingService.Data;
using Xunit;
using Maliev.Aspire.ServiceDefaults.Testing;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Maliev.PricingService.Tests.Integration;

public class PricingControllerTests : IClassFixture<PricingServiceTestFactory>
{
    private readonly PricingServiceTestFactory _factory;
    private readonly HttpClient _client;
    private readonly Mock<IMaterialServiceClient> _materialClientMock = new();

    public PricingControllerTests(PricingServiceTestFactory factory)
    {
        _factory = factory;
        _client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(_materialClientMock.Object);
            });
        }).CreateClient().WithTestAuth(permissions: [PricingPermissions.CalculationsCreate]);
    }

    [Fact]
    public async Task CalculatePrice_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var materialId = Guid.NewGuid();

        _materialClientMock.Setup(x => x.GetMaterialAsync(materialId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MaterialDto
            {
                Id = materialId,
                DensityGramPerCm3 = 1.24m,
                CostPerKg = 600,
                ProcessParameters = new Dictionary<string, string>()
            });

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = materialId,
            MaterialCode = "TEST-MAT",
            Technology = ManufacturingTechnology.Fdm,
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 10.0m,
                SupportVolumeCm3 = 2.0m,
                SurfaceAreaCm2 = 50.0m,
                BoundingBoxX = 10.0m,
                BoundingBoxY = 10.0m,
                BoundingBoxZ = 10.0m,
                IsManifold = true,
                TriangleCount = 1000
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/pricing/v1/pricing/calculate", request);

        // Assert
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Request failed with status {response.StatusCode}. Response: {error}");
        }

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PricingResult>();
        Assert.NotNull(result);
        Assert.True(result.TotalPrice > 0);
    }

    [Fact]
    public async Task CalculatePrice_WithMissingMaterial_ReturnsNotFound()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        _materialClientMock.Setup(x => x.GetMaterialAsync(materialId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MaterialDto?)null);

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = materialId,
            MaterialCode = "NON-EXISTENT",
            Technology = ManufacturingTechnology.Fdm,
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 10.0m,
                SupportVolumeCm3 = 2.0m,
                SurfaceAreaCm2 = 50.0m,
                BoundingBoxX = 10.0m,
                BoundingBoxY = 10.0m,
                BoundingBoxZ = 10.0m,
                IsManifold = true,
                TriangleCount = 1000
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/pricing/v1/pricing/calculate", request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
