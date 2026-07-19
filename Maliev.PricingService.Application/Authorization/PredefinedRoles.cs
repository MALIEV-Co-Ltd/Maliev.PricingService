namespace Maliev.PricingService.Application.Authorization;

/// <summary>
/// Provides access to predefined roles for the Pricing Service.
/// </summary>
public static class PricingPredefinedRoles
{
    public const string Admin = "roles.pricing.admin";
    public const string PricingAnalyst = "roles.pricing.pricing-analyst";
    public const string Viewer = "roles.pricing.viewer";

    public static readonly IReadOnlyList<(string RoleId, string Description, string[] Permissions)> All = new List<(string, string, string[])>
    {
        (
            Admin,
            "Pricing Administrator with full access",
            new[]
            {
                PricingPermissions.CalculationCreate,
                PricingPermissions.AuditRead,
                PricingPermissions.ConfigurationRead,
                PricingPermissions.ConfigurationCreate,
                PricingPermissions.ConfigurationUpdate,
                PricingPermissions.ConfigurationDelete,
                PricingPermissions.SnapshotCreate,
                PricingPermissions.SnapshotRead,
                PricingPermissions.SnapshotAccept,
                PricingPermissions.SnapshotSupersede,
            }
        ),
        (
            PricingAnalyst,
            "Pricing Analyst with calculation and snapshot access",
            new[]
            {
                PricingPermissions.CalculationCreate,
                PricingPermissions.AuditRead,
                PricingPermissions.ConfigurationRead,
                PricingPermissions.SnapshotCreate,
                PricingPermissions.SnapshotRead,
                PricingPermissions.SnapshotAccept,
            }
        ),
        (
            Viewer,
            "Pricing Viewer with read-only access",
            new[]
            {
                PricingPermissions.AuditRead,
                PricingPermissions.ConfigurationRead,
                PricingPermissions.SnapshotRead,
            }
        ),
    };
}
