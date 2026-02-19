using System.Net.Http.Json;
using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Data.Entities;
using Maliev.PricingService.Tests.TestFixtures;
using Microsoft.Extensions.DependencyInjection;
using Maliev.PricingService.Data;
using Xunit;
using Maliev.Aspire.ServiceDefaults.Testing;

namespace Maliev.PricingService.Tests.Integration;

public class PricingControllerTests : IClassFixture<PricingServiceTestFactory>
{
    private readonly PricingServiceTestFactory _factory;
    private readonly HttpClient _client;

    public PricingControllerTests(PricingServiceTestFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient().WithTestAuth(permissions: [PricingPermissions.CalculationsCreate]);
    }

    [Fact]
    public async Task CalculatePrice_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var materialId = Guid.NewGuid();
        var processId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
            db.PricingConfigurations.Add(new PricingConfiguration
            {
                MaterialId = materialId,
                ManufacturingProcessId = processId,
                MaterialPricePerCm3 = 10.0m,
                SupportMaterialPricePerCm3 = 5.0m,
                MachineHourlyRate = 50.0m,
                PrintSpeedCm3PerHour = 100.0m,
                SetupCostFlat = 25.0m,
                MarginMultiplier = 1.2m,
                EffectiveFrom = DateTime.UtcNow.AddDays(-1),
                CreatedBy = "Test"
            });
            await db.SaveChangesAsync();
        }

        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = materialId,
            MaterialCode = "TEST-MAT",
            ManufacturingProcessId = processId,
            ManufacturingProcessName = "TEST-PROC",
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
    public async Task CalculatePrice_WithMissingConfiguration_ReturnsBadRequest()
    {
        // Arrange
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "NON-EXISTENT",
            ManufacturingProcessId = Guid.NewGuid(),
            ManufacturingProcessName = "NON-EXISTENT",
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
