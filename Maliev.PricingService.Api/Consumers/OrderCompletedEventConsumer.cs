using Maliev.MessagingContracts.Contracts.Orders;
using Maliev.PricingService.Data;
using Maliev.PricingService.Data.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PricingService.Api.Consumers;

/// <summary>
/// Consumes OrderCompletedEvent to update training data with actual job outcomes.
/// This enables ML model improvement based on real production data.
/// </summary>
public class OrderCompletedEventConsumer : IConsumer<OrderCompletedEvent>
{
    private readonly PricingDbContext _dbContext;
    private readonly ILogger<OrderCompletedEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderCompletedEventConsumer"/> class.
    /// </summary>
    public OrderCompletedEventConsumer(
        PricingDbContext dbContext,
        ILogger<OrderCompletedEventConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        var payload = context.Message.Payload;
        var cancellationToken = context.CancellationToken;

        _logger.LogInformation(
            "Received OrderCompletedEvent for order {OrderId}, quotation {QuotationId}",
            payload.OrderId,
            payload.QuotationId);

        try
        {
            // Find the pricing audit record linked to this quotation
            var auditRecord = await _dbContext.PricingAuditRecords
                .Include(a => a.TrainingData)
                .FirstOrDefaultAsync(a => a.QuotationId == payload.QuotationId, cancellationToken);

            if (auditRecord == null)
            {
                _logger.LogWarning(
                    "No pricing audit record found for quotation {QuotationId}",
                    payload.QuotationId);
                return;
            }

            // Create or update training data
            var trainingData = auditRecord.TrainingData ?? new PricingTrainingData
            {
                Id = Guid.NewGuid(),
                PricingAuditRecordId = auditRecord.Id
            };

            trainingData.OrderId = payload.OrderId;
            trainingData.CustomerAccepted = true;
            trainingData.AcceptedAt = payload.OrderCreatedAt.UtcDateTime;
            trainingData.JobCompleted = true;
            trainingData.CompletedAt = payload.CompletedAt.UtcDateTime;
            trainingData.JobSucceeded = payload.JobSucceeded;
            trainingData.ActualMaterialUsedCm3 = payload.ActualMaterialUsedCm3.HasValue
                ? (decimal)payload.ActualMaterialUsedCm3.Value : null;
            trainingData.ActualPrintTimeHours = payload.ActualPrintTimeHours.HasValue
                ? (decimal)payload.ActualPrintTimeHours.Value : null;
            trainingData.ActualLaborHours = payload.ActualLaborHours.HasValue
                ? (decimal)payload.ActualLaborHours.Value : null;
            trainingData.ActualTotalCost = payload.ActualTotalCost.HasValue
                ? (decimal)payload.ActualTotalCost.Value : null;

            // Calculate actual profit margin
            if (payload.ActualTotalCost.HasValue && auditRecord.TotalPrice > 0)
            {
                trainingData.ActualProfitMargin =
                    (auditRecord.TotalPrice - (decimal)payload.ActualTotalCost.Value) / auditRecord.TotalPrice;
            }

            if (auditRecord.TrainingData == null)
            {
                _dbContext.PricingTrainingData.Add(trainingData);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Updated training data for audit record {AuditRecordId}, actual margin: {Margin:P2}",
                auditRecord.Id,
                trainingData.ActualProfitMargin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing OrderCompletedEvent for order {OrderId}",
                payload.OrderId);
            throw;
        }
    }
}
