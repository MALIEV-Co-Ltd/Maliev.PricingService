using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.PricingService.Application.DTOs;
using Maliev.PricingService.Application.Interfaces;
using Maliev.PricingService.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.PricingService.Api.Controllers;

/// <summary>
/// Controller for on-demand pricing calculations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("pricing/v{version:apiVersion}/calculate")]
public class PricingController : ControllerBase
{
    private readonly IPricingOrchestrator _orchestrator;
    private readonly ILogger<PricingController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PricingController"/> class.
    /// </summary>
    /// <param name="orchestrator">The pricing orchestrator.</param>
    /// <param name="logger">The logger.</param>
    public PricingController(IPricingOrchestrator orchestrator, ILogger<PricingController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Calculates pricing for a 3D model based on material and process.
    /// </summary>
    /// <param name="request">The pricing request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The pricing result.</returns>
    [HttpPost]
    [RequirePermission(PricingPermissions.CalculationsCreate)]
    [ProducesResponseType(typeof(PricingResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PricingResult>> CalculatePrice([FromBody] PricingRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received ad-hoc pricing request for FileId: {FileId}", request.FileId);
        var result = await _orchestrator.CalculatePriceAsync(request, cancellationToken);
        return Ok(result);
    }
}
