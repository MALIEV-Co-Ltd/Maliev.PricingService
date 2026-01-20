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
                VolumeCm3 = message.VolumeCm3,
                SupportVolumeCm3 = message.SupportVolumeCm3,
                SurfaceAreaCm2 = message.SurfaceAreaCm2,
                BoundingBoxX = message.BoundingBoxX,
                BoundingBoxY = message.BoundingBoxY,
                BoundingBoxZ = message.BoundingBoxZ,
                IsManifold = message.IsManifold,
                TriangleCount = message.TriangleCount
            },
            CorrelationId = context.CorrelationId?.ToString()
        };

        await _orchestrator.CalculatePriceAsync(pricingRequest, context.CancellationToken);

        _logger.LogInformation("Price calculation triggered for FileId: {FileId}", message.FileId);
    }
}
