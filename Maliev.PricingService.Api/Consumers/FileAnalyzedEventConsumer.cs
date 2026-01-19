namespace Maliev.PricingService.Api.Consumers;

using Maliev.MessagingContracts.Contracts.Geometry;
using Maliev.MessagingContracts.Contracts.Pricing;
using Maliev.PricingService.Api.Interfaces;
using Maliev.PricingService.Api.Services;
using MassTransit;
using Microsoft.Extensions.Logging;

/// <summary>
/// Consumes <see cref="FileAnalyzedEvent"/> from GeometryService and calculates pricing.
/// </summary>
public class FileAnalyzedEventConsumer : IConsumer<FileAnalyzedEvent>
{
    private readonly IPricingOrchestrator _orchestrator;
    private readonly ILogger<FileAnalyzedEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileAnalyzedEventConsumer"/> class.
    /// </summary>
    /// <param name="orchestrator">The pricing orchestrator.</param>
    /// <param name="logger">The logger.</param>
    public FileAnalyzedEventConsumer(IPricingOrchestrator orchestrator, ILogger<FileAnalyzedEventConsumer> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<FileAnalyzedEvent> context)
    {
        var message = context.Message;

        _logger.LogInformation("Processing FileAnalyzedEvent for FileId: {FileId}", message.FileId);

        // Map event metrics to pricing request
        // Assuming default material and process if not provided in metadata/context
        var pricingRequest = new PricingRequest
        {
            FileId = message.FileId,
            CustomerId = message.CustomerId,
            MaterialId = Guid.Empty, // Placeholder: should come from context or defaults
            MaterialCode = "DEFAULT",
            ManufacturingProcessId = Guid.Empty, // Placeholder
            ManufacturingProcessName = "DEFAULT",
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = (decimal)message.Volume,
                SupportVolumeCm3 = (decimal)message.SupportVolume,
                SurfaceAreaCm2 = (decimal)message.SurfaceArea,
                BoundingBoxX = (decimal)message.BoundingBoxX,
                BoundingBoxY = (decimal)message.BoundingBoxY,
                BoundingBoxZ = (decimal)message.BoundingBoxZ,
                IsManifold = message.IsManifold,
                TriangleCount = message.TriangleCount
            },
            CorrelationId = context.CorrelationId?.ToString()
        };

        var result = await _orchestrator.CalculatePriceAsync(pricingRequest, context.CancellationToken);

        // Publish result
        await context.Publish<PriceCalculatedEvent>(new
        {
            PricingAuditId = Guid.NewGuid(), // In real impl, this comes from DB save
            FileId = message.FileId,
            CustomerId = message.CustomerId,
            MaterialId = pricingRequest.MaterialId,
            ProcessId = pricingRequest.ManufacturingProcessId,
            Quantity = pricingRequest.Quantity,
            Strategy = result.Strategy.ToString(),
            Breakdown = new
            {
                MaterialCost = result.MaterialCost,
                SupportCost = result.SupportMaterialCost,
                MachineTimeCost = result.MachineTimeCost,
                SetupCost = result.SetupCost,
                ComplexitySurcharge = result.ComplexitySurcharge,
                MarginAmount = result.MarginAmount,
                TotalPrice = result.TotalPrice
            },
            Currency = "THB",
            ConfidenceLevel = result.ConfidenceLevel,
            ValidUntil = DateTime.UtcNow.AddDays(7),
            CalculatedAt = DateTime.UtcNow
        }, context.CancellationToken);

        _logger.LogInformation("Price calculated and published for FileId: {FileId}", message.FileId);
    }
}
