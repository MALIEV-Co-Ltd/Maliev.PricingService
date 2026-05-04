namespace Maliev.PricingService.Application.DTOs;

public record PricingRequest
{
    public Guid FileId { get; init; }
    public Guid CustomerId { get; init; }
    public Guid MaterialId { get; init; }
    public string MaterialCode { get; init; } = string.Empty;
    public Guid ManufacturingProcessId { get; init; }
    public string ManufacturingProcessName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Currency { get; init; } = "THB";
    public GeometryMetrics Geometry { get; init; } = new();
    public DfmMetrics? Dfm { get; init; }
    public Guid? CorrelationId { get; init; }
    public string? StoragePath { get; init; }
    public string? LeadTimeCode { get; init; }
    public string? ToleranceCode { get; init; }
    public decimal? ToleranceAdditionalCostPercent { get; init; }

    // ── Process-specific extensions (backwards-compatible nullables) ──────────
    public int? WeldLengthMm { get; init; }
    public int? CutLengthMm { get; init; }
    public int? BendCount { get; init; }
    public int? ElectrodeCount { get; init; }
    public int? PointCountThousands { get; init; }
    public int? LayerCount { get; init; }
    public decimal? WeightKg { get; init; }
    public decimal? ThicknessMm { get; init; }
    public decimal? VendorQuoteAmount { get; init; }
    public string? VendorQuoteCurrency { get; init; }
    public decimal? VendorQuoteMarkupOverride { get; init; }
}

public record GeometryMetrics
{
    public decimal VolumeCm3 { get; init; }
    public decimal SupportVolumeCm3 { get; init; }
    public decimal SurfaceAreaCm2 { get; init; }
    public decimal BoundingBoxX { get; init; }
    public decimal BoundingBoxY { get; init; }
    public decimal BoundingBoxZ { get; init; }
    public bool IsManifold { get; init; }
    public int TriangleCount { get; init; }
}

public record DfmMetrics
{
    public string ReportType { get; init; } = "FDM";
    public int ThinWallCount { get; init; }
    public bool SupportRequired { get; init; }
    public decimal? EstimatedSupportVolumeCm3 { get; init; }
    public bool ResinTrappingRisk { get; init; }
    public bool SuctionRisk { get; init; }
    public int SharpCornerCount { get; init; }
    public bool HasUndercuts { get; init; }
    public bool RequiresEdm { get; init; }
    public bool RequiresGrinding { get; init; }
}

public record PricingResult
{
    public decimal UnitPrice { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal UnitPriceBeforeVolumeDiscount { get; init; }
    public decimal VolumeDiscountUnitAmount { get; init; }
    public decimal VolumeDiscountPercent { get; init; }
    public decimal ConfidenceScore { get; init; }
    public string EngineName { get; init; } = string.Empty;
    public Guid AuditId { get; init; }
    public int EstimatedLeadTimeDays { get; init; }
}
