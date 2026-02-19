using Asp.Versioning;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.PricingService.Api.Models.Snapshots;
using Maliev.PricingService.Api.Services;
using Maliev.PricingService.Data;
using Maliev.PricingService.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Maliev.PricingService.Api.Controllers;

/// <summary>
/// Controller for managing pricing snapshots.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("pricing/v{version:apiVersion}/snapshots")]
public class PricingSnapshotsController : ControllerBase
{
    private readonly PricingDbContext _context;
    private readonly ILogger<PricingSnapshotsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PricingSnapshotsController"/> class.
    /// </summary>
    /// <param name="context">Database context.</param>
    /// <param name="logger">Logger instance.</param>
    public PricingSnapshotsController(PricingDbContext context, ILogger<PricingSnapshotsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new pricing snapshot.
    /// </summary>
    /// <param name="request">Snapshot details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created snapshot.</returns>
    [HttpPost]
    [RequirePermission(PricingPermissions.SnapshotsCreate)]
    [ProducesResponseType(typeof(PricingSnapshotResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PricingSnapshotResponse>> CreateSnapshot(
        [FromBody] CreatePricingSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var employeeId = User.FindFirst("sub")?.Value ?? "unknown";

        var snapshot = new PricingSnapshot
        {
            Id = Guid.NewGuid(),
            OrderId = request.OrderId,
            QuotationId = request.QuotationId,
            EmployeeId = employeeId,
            Technology = request.Technology,
            MaterialCode = request.MaterialCode,
            MaterialBrand = request.MaterialBrand,
            LayerHeight = request.LayerHeight,
            InfillPercentage = request.InfillPercentage,
            SupportType = request.SupportType,
            PrintOrientation = request.PrintOrientation,
            CalculatedPrice = request.CalculatedPrice,
            ManualOverridePrice = request.ManualOverridePrice,
            PricingAuditRecordId = request.PricingAuditRecordId,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow
        };

        _context.PricingSnapshots.Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetSnapshot), new { id = snapshot.Id, version = "1" }, MapToResponse(snapshot));
    }

    /// <summary>
    /// Retrieves a snapshot by ID.
    /// </summary>
    /// <param name="id">Snapshot ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Snapshot details.</returns>
    [HttpGet("{id:guid}")]
    [RequirePermission(PricingPermissions.SnapshotsRead)]
    [ProducesResponseType(typeof(PricingSnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PricingSnapshotResponse>> GetSnapshot(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await _context.PricingSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (snapshot == null)
            return NotFound();

        return Ok(MapToResponse(snapshot));
    }

    /// <summary>
    /// Lists snapshots for a specific order.
    /// </summary>
    /// <param name="orderId">Order ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of snapshots.</returns>
    [HttpGet]
    [RequirePermission(PricingPermissions.SnapshotsRead)]
    [ProducesResponseType(typeof(List<PricingSnapshotResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PricingSnapshotResponse>>> ListSnapshots(
        [FromQuery] string orderId,
        CancellationToken cancellationToken)
    {
        var snapshots = await _context.PricingSnapshots
            .AsNoTracking()
            .Where(s => s.OrderId == orderId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(snapshots.Select(MapToResponse).ToList());
    }

    /// <summary>
    /// Marks a snapshot as Accepted.
    /// </summary>
    /// <param name="id">Snapshot ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated snapshot.</returns>
    [HttpPatch("{id:guid}/accept")]
    [RequirePermission(PricingPermissions.SnapshotsAccept)]
    [ProducesResponseType(typeof(PricingSnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PricingSnapshotResponse>> AcceptSnapshot(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await _context.PricingSnapshots
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (snapshot == null)
            return NotFound();

        snapshot.Status = "Accepted";
        snapshot.AcceptedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(MapToResponse(snapshot));
    }

    /// <summary>
    /// Marks a snapshot as Superseded and links it to a newer snapshot.
    /// </summary>
    /// <param name="id">Snapshot ID to supersede.</param>
    /// <param name="supersededById">ID of the newer snapshot that supersedes this one.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated snapshot.</returns>
    [HttpPatch("{id:guid}/supersede")]
    [RequirePermission(PricingPermissions.SnapshotsSupersede)]
    [ProducesResponseType(typeof(PricingSnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PricingSnapshotResponse>> SupersedeSnapshot(
        Guid id,
        [FromQuery] Guid supersededById,
        CancellationToken cancellationToken)
    {
        if (id == supersededById)
            return BadRequest("A snapshot cannot supersede itself.");

        var snapshot = await _context.PricingSnapshots
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (snapshot == null)
            return NotFound();

        var supersedingExists = await _context.PricingSnapshots
            .AnyAsync(s => s.Id == supersededById, cancellationToken);

        if (!supersedingExists)
            return BadRequest($"Superseding snapshot {supersededById} not found.");

        snapshot.Status = "Superseded";
        snapshot.SupersededById = supersededById;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(MapToResponse(snapshot));
    }

    private static PricingSnapshotResponse MapToResponse(PricingSnapshot s)
    {
        return new PricingSnapshotResponse
        {
            Id = s.Id,
            OrderId = s.OrderId,
            QuotationId = s.QuotationId,
            EmployeeId = s.EmployeeId,
            Technology = s.Technology,
            MaterialCode = s.MaterialCode,
            MaterialBrand = s.MaterialBrand,
            LayerHeight = s.LayerHeight,
            InfillPercentage = s.InfillPercentage,
            SupportType = s.SupportType,
            PrintOrientation = s.PrintOrientation,
            CalculatedPrice = s.CalculatedPrice,
            ManualOverridePrice = s.ManualOverridePrice,
            PricingAuditRecordId = s.PricingAuditRecordId,
            Status = s.Status,
            SupersededById = s.SupersededById,
            CreatedAt = s.CreatedAt,
            AcceptedAt = s.AcceptedAt
        };
    }
}
