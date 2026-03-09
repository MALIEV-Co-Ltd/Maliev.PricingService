using Maliev.PricingService.Domain.Constants;
using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maliev.PricingService.Api.Services;

/// <summary>Service that handles registration of permissions and roles with the central IAM service on startup.</summary>
public class PricingIAMRegistrationService : IAMRegistrationService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PricingIAMRegistrationService"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="logger">The logger.</param>
    public PricingIAMRegistrationService(
        IConfiguration configuration,
        ILogger<PricingIAMRegistrationService> logger)
        : base(configuration, logger, "pricing")
    {
    }

    /// <summary>
    /// Gets the list of permissions to register with IAM.
    /// </summary>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return PricingPermissions.AllWithDescriptions.Select(p => new PermissionRegistration
        {
            PermissionId = p.Key,
            Description = p.Value
        });
    }

    /// <summary>
    /// Gets the list of predefined roles to register with IAM.
    /// </summary>
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
