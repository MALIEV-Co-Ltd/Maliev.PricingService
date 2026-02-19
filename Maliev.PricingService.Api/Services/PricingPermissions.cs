namespace Maliev.PricingService.Api.Services;

/// <summary>
/// Defines granular permission constants for the Pricing Service.
/// Follows GCP-style naming: {service}.{resource}.{action}
/// </summary>
public static class PricingPermissions
{
    /// <summary>Permission to calculate prices.</summary>
    public const string CalculationsCreate = "pricing.calculations.create";
    /// <summary>Permission to read pricing audit records.</summary>
    public const string AuditRead = "pricing.audit.read";
    /// <summary>Permission to read pricing configurations.</summary>
    public const string ConfigurationsRead = "pricing.configurations.read";
    /// <summary>Permission to create pricing configurations.</summary>
    public const string ConfigurationsCreate = "pricing.configurations.create";
    /// <summary>Permission to update pricing configurations.</summary>
    public const string ConfigurationsUpdate = "pricing.configurations.update";
    /// <summary>Permission to delete pricing configurations.</summary>
    public const string ConfigurationsDelete = "pricing.configurations.delete";
    /// <summary>Permission to read ML models.</summary>
    public const string ModelsRead = "pricing.models.read";
    /// <summary>Permission to train ML models.</summary>
    public const string ModelsTrain = "pricing.models.train";
    /// <summary>Permission to deploy ML models.</summary>
    public const string ModelsDeploy = "pricing.models.deploy";
    /// <summary>Permission to retire ML models.</summary>
    public const string ModelsRetire = "pricing.models.retire";

    // Snapshot Operations
    /// <summary>Permission to create pricing snapshots.</summary>
    public const string SnapshotsCreate = "pricing.snapshots.create";
    /// <summary>Permission to read pricing snapshots.</summary>
    public const string SnapshotsRead = "pricing.snapshots.read";
    /// <summary>Permission to accept pricing snapshots.</summary>
    public const string SnapshotsAccept = "pricing.snapshots.accept";
    /// <summary>Permission to supersede pricing snapshots.</summary>
    public const string SnapshotsSupersede = "pricing.snapshots.supersede";

    /// <summary>
    /// Collection of all defined pricing permissions with descriptions.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AllWithDescriptions = new Dictionary<string, string>
    {
        { CalculationsCreate, "Calculate instant prices for quotations" },
        { AuditRead, "Read pricing audit records and history" },
        { ConfigurationsRead, "Read pricing configurations" },
        { ConfigurationsCreate, "Create new pricing configurations" },
        { ConfigurationsUpdate, "Update existing pricing configurations" },
        { ConfigurationsDelete, "Delete pricing configurations" },
        { ModelsRead, "Read ML pricing model details" },
        { ModelsTrain, "Train new ML pricing models" },
        { ModelsDeploy, "Deploy ML pricing models to production" },
        { ModelsRetire, "Retire ML pricing models" },
        { SnapshotsCreate, "Create pricing snapshots" },
        { SnapshotsRead, "Read pricing snapshots" },
        { SnapshotsAccept, "Accept pricing snapshots" },
        { SnapshotsSupersede, "Supersede pricing snapshots with a newer version" }
    };

    /// <summary>
    /// Gets all defined permission codes.
    /// </summary>
    public static string[] All => AllWithDescriptions.Keys.ToArray();
}

/// <summary>
/// Provides access to predefined roles for the Pricing Service.
/// </summary>
public static class PricingPredefinedRoles
{
    /// <summary>Role for administrators with full control.</summary>
    public const string Admin = "roles.pricing.admin";
    /// <summary>Role for pricing analysts who manage configurations.</summary>
    public const string Analyst = "roles.pricing.analyst";
    /// <summary>Role for users with read-only access.</summary>
    public const string Viewer = "roles.pricing.viewer";
    /// <summary>Role for services that can calculate prices.</summary>
    public const string Calculator = "roles.pricing.calculator";

    /// <summary>
    /// Collection of all predefined roles for the Pricing Service.
    /// </summary>
    public static readonly IReadOnlyList<(string RoleId, string Description, string[] Permissions)> All = new List<(string, string, string[])>
    {
        (Admin, "Full administrative control over pricing service", PricingPermissions.All),
        (Analyst, "Manage pricing configurations and view audit records", new[]
        {
            PricingPermissions.CalculationsCreate,
            PricingPermissions.AuditRead,
            PricingPermissions.ConfigurationsRead,
            PricingPermissions.ConfigurationsCreate,
            PricingPermissions.ConfigurationsUpdate,
            PricingPermissions.ModelsRead
        }),
        (Viewer, "Read-only access to pricing data and audit records", new[]
        {
            PricingPermissions.AuditRead,
            PricingPermissions.ConfigurationsRead,
            PricingPermissions.ModelsRead
        }),
        (Calculator, "Calculate prices for quotations (for service-to-service calls)", new[]
        {
            PricingPermissions.CalculationsCreate,
            PricingPermissions.AuditRead
        })
    };
}
