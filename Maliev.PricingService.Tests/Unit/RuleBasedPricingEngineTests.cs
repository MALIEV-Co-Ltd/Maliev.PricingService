using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Api.Services.Calculators;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Maliev.PricingService.Tests.Unit;

public class RuleBasedPricingEngineTests
{
    private readonly RuleBasedPricingEngine _sut;
    private readonly Mock<IPricingCalculator> _calculatorMock = new();

    public RuleBasedPricingEngineTests()
    {
        _calculatorMock.Setup(x => x.Technology).Returns(ManufacturingTechnology.Fdm);
        _sut = new RuleBasedPricingEngine(new[] { _calculatorMock.Object });
    }

    [Fact]
    public async Task CalculateAsync_DelegatesToCorrectCalculator()
    {
        // Arrange
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "MAT",
            Technology = ManufacturingTechnology.Fdm,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = 10,
                SupportVolumeCm3 = 2,
                SurfaceAreaCm2 = 100,
                BoundingBoxX = 10,
                BoundingBoxY = 10,
                BoundingBoxZ = 10,
                IsManifold = true,
                TriangleCount = 100
            },
            Quantity = 1
        };

        var material = new MaterialData { Density = 1.24m, CostPerKg = 600 };
        var rates = new MachineRates { FdmMachineHourlyRate = 150 };

        var expectedResult = new PricingResult
        {
            Strategy = PricingStrategy.RuleBased,
            MaterialCost = 100,
            SupportMaterialCost = 0,
            MachineTimeCost = 50,
            SetupCost = 20,
            ComplexitySurcharge = 0,
            SubtotalBeforeMargin = 170,
            MarginAmount = 30,
            TotalUnitPrice = 200,
            TotalPrice = 200,
            ConfidenceLevel = 1.0m,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            CalculationDuration = TimeSpan.Zero
        };

        _calculatorMock.Setup(x => x.CalculateAsync(request, material, rates, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.CalculateAsync(request, material, rates);

        // Assert
        Assert.Equal(expectedResult.TotalUnitPrice, result.TotalUnitPrice);
        _calculatorMock.Verify(x => x.CalculateAsync(request, material, rates, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CalculateAsync_ThrowsWhenNoCalculatorFound()
    {
        // Arrange
        var request = new PricingRequest
        {
            FileId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            MaterialId = Guid.NewGuid(),
            MaterialCode = "MAT",
            Technology = ManufacturingTechnology.Sla, // No SLA calculator in SUT
            Geometry = new GeometryMetrics { VolumeCm3 = 1, SupportVolumeCm3 = 0, SurfaceAreaCm2 = 1, BoundingBoxX = 1, BoundingBoxY = 1, BoundingBoxZ = 1, IsManifold = true, TriangleCount = 1 }
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.CalculateAsync(request, new MaterialData(), new MachineRates()));
    }
}
