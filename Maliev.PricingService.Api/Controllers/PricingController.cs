using Asp.Versioning;
using Maliev.PricingService.Api.Interfaces;
using Maliev.PricingService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.PricingService.Api.Controllers;

/// <summary>
/// Controller for pricing calculations.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/pricing")]
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
    /// Calculates the price for a given set of inputs.
    /// </summary>
    /// <param name="request">The pricing request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The pricing result.</returns>
    [HttpPost("calculate")]
    [ProducesResponseType(typeof(PricingResult), 200)]
    [ProducesResponseType(400)]
    [Authorize(Policy = PricingPermissions.CalculationsCreate)]
    public async Task<ActionResult<PricingResult>> CalculatePrice([FromBody] PricingRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received ad-hoc pricing request for FileId: {FileId}", request.FileId);

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _orchestrator.CalculatePriceAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid pricing request: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during pricing calculation");
            return StatusCode(500, new { message = "An internal error occurred during calculation." });
        }
    }
}
