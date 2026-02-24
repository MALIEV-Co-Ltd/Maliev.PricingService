namespace Maliev.PricingService.Tests.Unit.Calculators;

using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Api.Services.Calculators;
using Maliev.PricingService.Api.Clients;
using Moq;
using System.Threading.Tasks;
using Xunit;

public class ScanningAndDesignCalculatorTests
{
    private readonly Mock<IChatbotServiceClient> _chatbotClientMock = new();

    [Fact]
    public async Task ScanningCalculateAsync_ReturnsCorrectTierPrice()
    {
        var calculator = new ScanningPricingCalculator();
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "SCAN",
            Technology = ManufacturingTechnology.Scanning,
            ScanningTier = "RawScan",
            Quantity = 1,
            Geometry = new GeometryMetrics { VolumeCm3 = 1, SupportVolumeCm3 = 0, SurfaceAreaCm2 = 1, BoundingBoxX = 1, BoundingBoxY = 1, BoundingBoxZ = 1, IsManifold = true, TriangleCount = 1 }
        };

        var result = await calculator.CalculateAsync(request, new MaterialData(), new MachineRates());
        Assert.Equal(2500m, result.TotalUnitPrice);

        var request2 = request with { ScanningTier = "ReverseEngineering" };
        var result2 = await calculator.CalculateAsync(request2, new MaterialData(), new MachineRates());
        Assert.Equal(4500m, result2.TotalUnitPrice);
    }

    [Fact]
    public async Task DesignCalculateAsync_ReturnsFixedPrice()
    {
        var calculator = new DesignPricingCalculator(_chatbotClientMock.Object);
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "DESIGN",
            Technology = ManufacturingTechnology.Design,
            Quantity = 1,
            Geometry = new GeometryMetrics { VolumeCm3 = 1, SupportVolumeCm3 = 0, SurfaceAreaCm2 = 1, BoundingBoxX = 1, BoundingBoxY = 1, BoundingBoxZ = 1, IsManifold = true, TriangleCount = 1 }
        };

        var result = await calculator.CalculateAsync(request, new MaterialData(), new MachineRates());
        Assert.Equal(500m, result.TotalUnitPrice);
    }

    [Fact]
    public async Task DesignCalculateAsync_WithAnalysis_ReturnsCalculatedPrice()
    {
        // Arrange
        var calculator = new DesignPricingCalculator(_chatbotClientMock.Object);
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "DESIGN",
            Technology = ManufacturingTechnology.Design,
            DesignDescription = "Complex character model",
            ReferenceImageUrls = new System.Collections.Generic.List<string> { "http://example.com/image.jpg" },
            Quantity = 1,
            Geometry = new GeometryMetrics { VolumeCm3 = 1, SupportVolumeCm3 = 0, SurfaceAreaCm2 = 1, BoundingBoxX = 1, BoundingBoxY = 1, BoundingBoxZ = 1, IsManifold = true, TriangleCount = 1 }
        };

        var analysisResult = new Maliev.PricingService.Api.DTOs.DesignAnalysisResult
        {
            ModelingTechnique = "MeshSculpting",
            EstimatedDesignHours = 10m,
            AdditionalCommunicationHours = 2m,
            EstimatedDrawingHours = 0m,
            RequiresPreScan = true,
            InformationCompleteness = "Complete",
            RecommendedSoftware = "ZBrush",
            ConfidenceLevel = 0.9m,
            Notes = "Art character"
        };

        _chatbotClientMock.Setup(c => c.AnalyzeDesignAsync(
            It.IsAny<System.Collections.Generic.List<string>>(),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(analysisResult);

        var rates = new MachineRates
        {
            DesignHourlyRate = 350m,
            ScanningBasePrice = 2500m
        };

        // Act
        var result = await calculator.CalculateAsync(request, new MaterialData(), rates);

        // Assert
        // baseDesignHours = 10 + 2 + 0 = 12
        // techniqueMultiplier (MeshSculpting) = 1.8
        // designCost = 12 * 350 * 1.8 = 4200 * 1.8 = 7560
        // scanningCost = 2500
        // total = 7560 + 2500 = 10060
        Assert.Equal(10060m, result.TotalUnitPrice);
        Assert.Equal(7560m, result.MachineTimeCost); // Labor
        Assert.Equal(2500m, result.SetupCost);       // Pre-scan
        Assert.Equal(0.9m, result.ConfidenceLevel);
        Assert.Contains("MeshSculpting", result.Notes);
    }
}
