using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Maliev.PricingService.Application.Services;

public class PricingOrchestrator : IPricingOrchestrator
{
    private readonly IPricingDbContext _context;
    private readonly IPricingEngine _ruleEngine;
    private readonly IMLPricingEngine _mlEngine;
    private readonly ILogger<PricingOrchestrator> _logger;

    public PricingOrchestrator(
        IPricingDbContext context,
        IPricingEngine ruleEngine,
        IMLPricingEngine mlEngine,
        ILogger<PricingOrchestrator> logger)
    {
        _context = context;
        _ruleEngine = ruleEngine;
        _mlEngine = mlEngine;
        _logger = logger;
    }

    public async Task<PricingResult> CalculatePriceAsync(PricingRequest request, CancellationToken cancellationToken = default)
    {
        // For migration purpose, use a placeholder configuration
        var config = new PricingConfiguration 
        { 
            Id = Guid.NewGuid(),
            MaterialId = Guid.Empty,
            ManufacturingProcessId = Guid.Empty,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow
        };
        
        var ruleResult = await _ruleEngine.CalculateAsync(request, config, cancellationToken);
        var mlResult = await _mlEngine.CalculateAsync(request, config, cancellationToken);

        // Logic to combine results
        var finalResult = mlResult.ConfidenceScore > 0.8m ? mlResult : ruleResult;

        var auditRecord = new PricingAuditRecord
        {
            Id = Guid.NewGuid(),
            FileId = Guid.Empty, // Placeholder
            CustomerId = Guid.Empty, // Placeholder
            MaterialCode = request.MaterialCode,
            Quantity = (int)request.Quantity,
            Strategy = finalResult.EngineName.Contains("ML") ? PricingStrategy.MLEnhanced : PricingStrategy.RuleBased,
            MLModelVersion = finalResult.EngineName,
            TotalUnitPrice = finalResult.UnitPrice,
            TotalPrice = finalResult.TotalAmount,
            ConfidenceLevel = finalResult.ConfidenceScore,
            CalculatedAt = DateTime.UtcNow,
            CalculationDuration = TimeSpan.FromMilliseconds(100) // Placeholder
        };

        _context.AuditRecords.Add(auditRecord);

        _context.Snapshots.Add(new PricingSnapshot
        {
            Id = Guid.NewGuid(),
            OrderId = "TEMP", // Placeholder
            EmployeeId = "SYSTEM", // Placeholder
            MaterialCode = request.MaterialCode,
            CalculatedPrice = finalResult.TotalAmount,
            PricingAuditRecordId = auditRecord.Id,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        return finalResult;
    }

    public async Task<PricingResult> AuditCalculationAsync(Guid calculationId, PricingResult result, CancellationToken cancellationToken = default)
    {
        // Simple audit for result consistency
        _context.AuditRecords.Add(new PricingAuditRecord
        {
            Id = Guid.NewGuid(),
            FileId = Guid.Empty,
            CustomerId = Guid.Empty,
            TotalUnitPrice = result.UnitPrice,
            TotalPrice = result.TotalAmount,
            CalculatedAt = DateTime.UtcNow,
            CalculationDuration = TimeSpan.Zero,
            Strategy = PricingStrategy.Manual
        });

        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }
}
