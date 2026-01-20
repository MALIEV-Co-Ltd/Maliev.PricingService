using Maliev.Aspire.ServiceDefaults.IAM;
using RoleRegistration = Maliev.Aspire.ServiceDefaults.IAM.RoleRegistration;

namespace Maliev.PricingService.Api.Services;

/// <summary>Service that handles registration of permissions and roles with the central IAM service on startup.</summary>
public class PricingIAMRegistrationService : IAMRegistrationService
{
    /// <summary>Initializes a new instance of the <see cref="PricingIAMRegistrationService"/> class.</summary>
    public PricingIAMRegistrationService(
        IConfiguration configuration,
        ILogger<PricingIAMRegistrationService> logger)
        : base(configuration, logger, "pricing")
    {
    }

    /// <summary>Gets the list of permissions to register with IAM.</summary>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return PricingPermissions.AllWithDescriptions.Select(p => new PermissionRegistration
        {
            PermissionId = p.Key,
            Description = p.Value
        });
    }

    /// <summary>Gets the list of predefined roles to register with IAM.</summary>
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return PricingPredefinedRoles.All.Select(r => new RoleRegistration
        {
            RoleId = r.RoleId,
            Description = r.Description,
            PermissionIds = r.Permissions.ToList(),
            IsCustom = false
        });
    }
}
