namespace Maliev.PricingService.Api.Consumers;

using Maliev.MessagingContracts.Contracts.Geometry;
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
        var payload = context.Message.Payload;

        _logger.LogInformation("Processing FileAnalyzedEvent for FileId: {FileId}", payload.FileId);

        var pricingRequest = new PricingRequest
        {
            FileId = Guid.Parse(payload.FileId),
            CustomerId = payload.CustomerId,
            MaterialId = Guid.Empty, // Placeholder: should come from context or defaults
            MaterialCode = "DEFAULT",
            ManufacturingProcessId = Guid.Empty, // Placeholder
            ManufacturingProcessName = "DEFAULT",
            Quantity = 1,
            Geometry = new GeometryMetrics
            {
                VolumeCm3 = (decimal)payload.Metrics.VolumeCm3,
                SupportVolumeCm3 = (decimal)payload.Metrics.SupportVolumeCm3,
                SurfaceAreaCm2 = (decimal)payload.Metrics.SurfaceAreaCm2,
                BoundingBoxX = (decimal)payload.Metrics.BoundingBox.X,
                BoundingBoxY = (decimal)payload.Metrics.BoundingBox.Y,
                BoundingBoxZ = (decimal)payload.Metrics.BoundingBox.Z,
                IsManifold = payload.Metrics.IsManifold,
                TriangleCount = payload.Metrics.TriangleCount
            },
            CorrelationId = context.CorrelationId?.ToString()
        };

        await _orchestrator.CalculatePriceAsync(pricingRequest, context.CancellationToken);

        _logger.LogInformation("Price calculation triggered for FileId: {FileId}", payload.FileId);
    }
}
