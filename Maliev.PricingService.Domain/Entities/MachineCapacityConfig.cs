namespace Maliev.PricingService.Domain.Entities;

/// <summary>
/// Represents machine capacity configuration used to estimate production lead times.
/// </summary>
public class MachineCapacityConfig
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Manufacturing process type (e.g. "FDM", "SLA", "CNC").</summary>
    public string ProcessType { get; set; } = string.Empty;

    /// <summary>Number of machines available for this process type.</summary>
    public int MachineCount { get; set; }

    /// <summary>Average throughput in parts per day across all machines.</summary>
    public decimal AvgThroughputPartsPerDay { get; set; }

    /// <summary>Current number of parts in the production queue; updated by user with real data.</summary>
    public int CurrentQueueDepth { get; set; }

    /// <summary>Number of days required for setup before production begins.</summary>
    public int SetupTimeDays { get; set; }

    /// <summary>Buffer days added for shipping.</summary>
    public int ShippingBufferDays { get; set; }

    /// <summary>Whether this configuration is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Timestamp when created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Timestamp when last updated.</summary>
    public DateTime? UpdatedAt { get; set; }
}
