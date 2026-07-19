namespace Maliev.PricingService.Application.Authorization;

/// <summary>
/// Defines the permissions for the Pricing Service.
/// </summary>
public static class PricingPermissions
{
    public const string CalculationCreate = "pricing.calculations.create";

    public const string AuditRead = "pricing.audit.read";

    public const string ConfigurationRead = "pricing.configurations.read";
    public const string ConfigurationCreate = "pricing.configurations.create";
    public const string ConfigurationUpdate = "pricing.configurations.update";
    public const string ConfigurationDelete = "pricing.configurations.delete";

    public const string SnapshotCreate = "pricing.snapshots.create";
    public const string SnapshotRead = "pricing.snapshots.read";
    public const string SnapshotAccept = "pricing.snapshots.accept";
    public const string SnapshotSupersede = "pricing.snapshots.supersede";

    public static readonly IReadOnlyDictionary<string, string> AllWithDescriptions = new Dictionary<string, string>
    {
        { CalculationCreate, "Create pricing calculations" },
        { AuditRead, "Read pricing audit logs" },
        { ConfigurationRead, "Read pricing configurations" },
        { ConfigurationCreate, "Create pricing configurations" },
        { ConfigurationUpdate, "Update pricing configurations" },
        { ConfigurationDelete, "Delete pricing configurations" },
        { SnapshotCreate, "Create pricing snapshots" },
        { SnapshotRead, "Read pricing snapshots" },
        { SnapshotAccept, "Accept pricing snapshots" },
        { SnapshotSupersede, "Supersede pricing snapshots" },
    };

    public static string[] All => AllWithDescriptions.Keys.ToArray();
}
