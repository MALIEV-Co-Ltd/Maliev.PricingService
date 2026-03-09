using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Domain.Entities;
using Maliev.MessagingContracts.Contracts.Orders;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.PricingService.Api.Consumers;

/// <summary>
/// Consumes <see cref="OrderCompletedEvent"/> to finalise pricing audit logs and transition snapshots.
/// </summary>
public class OrderCompletedEventConsumer : IConsumer<OrderCompletedEvent>
{
    private readonly IPricingDbContext _context;
    private readonly ILogger<OrderCompletedEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderCompletedEventConsumer"/> class.
    /// </summary>
    /// <param name="context">The pricing database context.</param>
    /// <param name="logger">The logger.</param>
    public OrderCompletedEventConsumer(IPricingDbContext context, ILogger<OrderCompletedEventConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Consumes the specified context.
    /// </summary>
    /// <param name="context">The context.</param>
    public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        var payload = context.Message.Payload;
        _logger.LogInformation("Received OrderCompletedEvent for order {OrderId}", payload.OrderId);

        var auditRecord = await _context.AuditRecords
            .FirstOrDefaultAsync(a => a.CorrelationId == context.CorrelationId.ToString());

        if (auditRecord != null)
        {
            auditRecord.CalculatedBySystem = "PricingService-Finalized";
            await _context.SaveChangesAsync(context.CancellationToken);
        }
    }
}
