using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Maliev.PricingService.Api.Controllers;
using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Application.Services;
using Maliev.PricingService.Domain.Entities;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using DataAnnotationsValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace Maliev.PricingService.Tests.Unit;

public sealed class PricingRequestQuantityValidationTests
{
    [Fact]
    public void Validate_OmittedQuantity_ReturnsQuantityFieldError()
    {
        var request = new PricingRequest();

        var errors = Validate(request);

        AssertQuantityError(errors);
    }

    [Fact]
    public void Validate_ExplicitZeroQuantity_ReturnsQuantityFieldError()
    {
        var request = new PricingRequest { Quantity = 0m };

        var errors = Validate(request);

        AssertQuantityError(errors);
    }

    [Fact]
    public void Validate_NegativeQuantity_ReturnsQuantityFieldError()
    {
        var request = new PricingRequest { Quantity = -1m };

        var errors = Validate(request);

        AssertQuantityError(errors);
    }

    [Fact]
    public void Validate_QuantityOne_HasNoQuantityFieldError()
    {
        var request = new PricingRequest { Quantity = 1m };

        var errors = Validate(request);

        Assert.DoesNotContain(errors, HasQuantityMember);
    }

    [Fact]
    public void Quantity_UsesRequiredPositiveDecimalRangeAnnotations()
    {
        var property = typeof(PricingRequest).GetProperty(nameof(PricingRequest.Quantity));

        Assert.NotNull(property);
        Assert.NotNull(property.GetCustomAttribute<RequiredAttribute>());
        var range = property.GetCustomAttribute<RangeAttribute>();
        Assert.NotNull(range);
        Assert.Equal(typeof(decimal), range.OperandType);
        Assert.Equal("1", range.Minimum);
        Assert.Equal(int.MaxValue.ToString(), range.Maximum);
    }

    [Fact]
    public void PricingController_UsesApiControllerBodyBindingForPricingRequest()
    {
        Assert.NotNull(typeof(PricingController).GetCustomAttribute<ApiControllerAttribute>());
        var action = typeof(PricingController).GetMethod(nameof(PricingController.CalculatePrice));
        Assert.NotNull(action);
        var requestParameter = Assert.Single(
            action.GetParameters(),
            parameter => parameter.ParameterType == typeof(PricingRequest));
        Assert.NotNull(requestParameter.GetCustomAttribute<FromBodyAttribute>());
    }

    private static IReadOnlyList<DataAnnotationsValidationResult> Validate(PricingRequest request)
    {
        var errors = new List<DataAnnotationsValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);
        return errors;
    }

    private static void AssertQuantityError(IReadOnlyList<DataAnnotationsValidationResult> errors)
    {
        Assert.Contains(errors, HasQuantityMember);
    }

    private static bool HasQuantityMember(DataAnnotationsValidationResult result)
    {
        return result.MemberNames.Contains(nameof(PricingRequest.Quantity), StringComparer.Ordinal);
    }
}

public sealed class PricingOrchestratorQuantityGuardTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CalculatePriceAsync_QuantityBelowOne_ReturnsNoPriceWithoutQueryOrEngineCall(int quantity)
    {
        var context = new Mock<IPricingDbContext>(MockBehavior.Strict);
        context
            .SetupGet(candidate => candidate.Configurations)
            .Throws(new InvalidOperationException("Pricing configuration query must not run for an invalid quantity."));
        var engine = new Mock<IPricingEngine>(MockBehavior.Strict);
        var logger = new Mock<ILogger<PricingOrchestrator>>();
        var orchestrator = new PricingOrchestrator(
            context.Object,
            engine.Object,
            new Mock<IJobServiceClient>(MockBehavior.Strict).Object,
            logger.Object,
            new Mock<IPublishEndpoint>(MockBehavior.Strict).Object,
            new Mock<IVolumeDiscountResolver>(MockBehavior.Strict).Object,
            new Mock<ICurrencyServiceClient>(MockBehavior.Strict).Object);

        var result = await orchestrator.CalculatePriceAsync(CreateRequest(quantity));

        Assert.Equal(0m, result.UnitPrice);
        Assert.Equal(0m, result.TotalAmount);
        Assert.Equal(1m, result.ConfidenceScore);
        Assert.Equal("None", result.EngineName);
        Assert.Equal(Guid.Empty, result.AuditId);
        Assert.Equal(0, result.EstimatedLeadTimeDays);
        engine.Verify(candidate => candidate.CalculateAsync(
            It.IsAny<PricingRequest>(),
            It.IsAny<PricingConfiguration>(),
            It.IsAny<CancellationToken>()), Times.Never);
        VerifyLogContains(logger, LogLevel.Warning, "invalid quantity");
    }

    private static PricingRequest CreateRequest(int quantity) => new()
    {
        FileId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        MaterialId = Guid.NewGuid(),
        MaterialCode = "PLA",
        ManufacturingProcessId = Guid.NewGuid(),
        ManufacturingProcessName = "FDM",
        Quantity = quantity,
    };

    private static void VerifyLogContains(
        Mock<ILogger<PricingOrchestrator>> logger,
        LogLevel level,
        string expectedText)
    {
        logger.Verify(candidate => candidate.Log(
            level,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains(expectedText, StringComparison.OrdinalIgnoreCase)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
