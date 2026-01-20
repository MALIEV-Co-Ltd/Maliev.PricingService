using Maliev.MessagingContracts.Contracts;
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
        var message = context.Message;
        var cancellationToken = context.CancellationToken;

        _logger.LogInformation(
            "Received OrderCompletedEvent for order {OrderId}, quotation {QuotationId}",
            message.OrderId,
            message.QuotationId);

        try
        {
            // Find the pricing audit record linked to this quotation
            var auditRecord = await _dbContext.PricingAuditRecords
                .Include(a => a.TrainingData)
                .FirstOrDefaultAsync(a => a.QuotationId == message.QuotationId, cancellationToken);

            if (auditRecord == null)
            {
                _logger.LogWarning(
                    "No pricing audit record found for quotation {QuotationId}",
                    message.QuotationId);
                return;
            }

            // Create or update training data
            var trainingData = auditRecord.TrainingData ?? new PricingTrainingData
            {
                Id = Guid.NewGuid(),
                PricingAuditRecordId = auditRecord.Id
            };

            trainingData.OrderId = message.OrderId;
            trainingData.CustomerAccepted = true;
            trainingData.AcceptedAt = message.OrderCreatedAt;
            trainingData.JobCompleted = true;
            trainingData.CompletedAt = message.CompletedAt;
            trainingData.JobSucceeded = message.JobSucceeded;
            trainingData.ActualMaterialUsedCm3 = message.ActualMaterialUsedCm3;
            trainingData.ActualPrintTimeHours = message.ActualPrintTimeHours;
            trainingData.ActualLaborHours = message.ActualLaborHours;
            trainingData.ActualTotalCost = message.ActualTotalCost;

            // Calculate actual profit margin
            if (message.ActualTotalCost.HasValue && auditRecord.TotalPrice > 0)
            {
                trainingData.ActualProfitMargin =
                    (auditRecord.TotalPrice - message.ActualTotalCost.Value) / auditRecord.TotalPrice;
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
                message.OrderId);
            throw;
        }
    }
}
