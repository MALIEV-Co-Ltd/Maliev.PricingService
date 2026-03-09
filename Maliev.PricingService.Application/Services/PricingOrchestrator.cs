using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
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
        var config = await _context.Configurations
            .Where(c => c.MaterialId == request.MaterialId
                     && c.ManufacturingProcessId == request.ManufacturingProcessId
                     && c.IsActive
                     && c.EffectiveFrom <= DateTime.UtcNow
                     && (c.EffectiveTo == null || c.EffectiveTo >= DateTime.UtcNow))
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (config == null)
        {
            _logger.LogWarning(
                "No active pricing configuration found for MaterialId: {MaterialId}, ManufacturingProcessId: {ManufacturingProcessId}",
                request.MaterialId,
                request.ManufacturingProcessId);

            return new PricingResult
            {
                UnitPrice = 0,
                TotalAmount = 0,
                ConfidenceScore = 0,
                EngineName = "None"
            };
        }

        var ruleResult = await _ruleEngine.CalculateAsync(request, config, cancellationToken);
        var mlResult = await _mlEngine.CalculateAsync(request, config, cancellationToken);

        var finalResult = mlResult.ConfidenceScore > 0.8m ? mlResult : ruleResult;

        var now = DateTime.UtcNow;
        var auditRecord = new PricingAuditRecord
        {
            Id = Guid.NewGuid(),
            FileId = request.FileId,
            CustomerId = request.CustomerId,
            MaterialId = request.MaterialId,
            ManufacturingProcessId = request.ManufacturingProcessId,
            MaterialCode = request.MaterialCode,
            ManufacturingProcessName = request.ManufacturingProcessName,
            Quantity = (int)request.Quantity,
            InputVolumeCm3 = request.Geometry.VolumeCm3,
            InputSupportVolumeCm3 = request.Geometry.SupportVolumeCm3,
            InputSurfaceAreaCm2 = request.Geometry.SurfaceAreaCm2,
            InputBoundingBoxX = request.Geometry.BoundingBoxX,
            InputBoundingBoxY = request.Geometry.BoundingBoxY,
            InputBoundingBoxZ = request.Geometry.BoundingBoxZ,
            InputIsManifold = request.Geometry.IsManifold,
            InputTriangleCount = request.Geometry.TriangleCount,
            PricingConfigurationId = config.Id,
            ConfigMaterialPricePerCm3 = config.MaterialPricePerCm3,
            ConfigSupportPricePerCm3 = config.SupportMaterialPricePerCm3,
            ConfigMachineHourlyRate = config.MachineHourlyRate,
            ConfigMarginMultiplier = config.MarginMultiplier,
            Strategy = finalResult.EngineName.Contains("ML") ? PricingStrategy.MLEnhanced : PricingStrategy.RuleBased,
            MLModelVersion = finalResult.EngineName,
            TotalUnitPrice = finalResult.UnitPrice,
            TotalPrice = finalResult.TotalAmount,
            ConfidenceLevel = finalResult.ConfidenceScore,
            CurrencyCode = request.Currency,
            ValidFrom = now,
            ValidUntil = now.AddDays(30),
            CalculatedAt = now,
            CalculationDuration = TimeSpan.FromMilliseconds(100),
            CorrelationId = request.CorrelationId?.ToString()
        };

        _context.AuditRecords.Add(auditRecord);

        _context.Snapshots.Add(new PricingSnapshot
        {
            Id = Guid.NewGuid(),
            OrderId = "TEMP",
            EmployeeId = "SYSTEM",
            MaterialCode = request.MaterialCode,
            CalculatedPrice = finalResult.TotalAmount,
            PricingAuditRecordId = auditRecord.Id,
            CreatedAt = now
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
