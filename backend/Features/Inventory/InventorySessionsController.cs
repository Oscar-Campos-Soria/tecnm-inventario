using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Inventory.AdjustInventoryRecord;
using TecnmInventario.Api.Features.Inventory.CancelInventorySession;
using TecnmInventario.Api.Features.Inventory.CloseInventorySession;
using TecnmInventario.Api.Features.Inventory.CreateInventorySession;
using TecnmInventario.Api.Features.Inventory.GetInventorySessionById;
using TecnmInventario.Api.Features.Inventory.GetInventorySessions;
using TecnmInventario.Api.Features.Inventory.MarkInventoryRecordNotFound;
using TecnmInventario.Api.Features.Inventory.ScanInventoryAsset;

namespace TecnmInventario.Api.Features.Inventory;

/// <summary>Levantamiento físico (specs/inventory.spec.md).</summary>
[ApiController]
[Route("api/inventory-sessions")]
[Authorize(Policy = "inventory.manage")]
public class InventorySessionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventorySessionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InventorySessionSummary>>> GetSessions(
        [FromQuery] PagedRequest pagination, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetInventorySessionsQuery(pagination.PageNumber, pagination.PageSize), cancellationToken));

    [HttpGet("{sessionId:long}")]
    public async Task<ActionResult<InventorySessionDetail>> GetSession(long sessionId, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetInventorySessionByIdQuery(sessionId), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CreateInventorySessionResponse>> CreateSession(
        CreateInventorySessionCommand command, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created, await _mediator.Send(command, cancellationToken));

    public sealed record ScanBody(string InventoryCode, long? LocationFoundId, string? Notes);

    [HttpPost("{sessionId:long}/scan")]
    public async Task<ActionResult<ScanInventoryAssetResponse>> Scan(
        long sessionId, ScanBody body, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new ScanInventoryAssetCommand(sessionId, body.InventoryCode, body.LocationFoundId, body.Notes), cancellationToken));

    [HttpPost("{sessionId:long}/records/{recordId:long}/not-found")]
    public async Task<ActionResult<MarkInventoryRecordNotFoundResponse>> MarkNotFound(
        long sessionId, long recordId, [FromQuery] int expectedVersion, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new MarkInventoryRecordNotFoundCommand(sessionId, recordId, expectedVersion), cancellationToken));

    [HttpPost("{sessionId:long}/close")]
    public async Task<ActionResult<CloseInventorySessionResponse>> Close(
        long sessionId, [FromQuery] int expectedVersion, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new CloseInventorySessionCommand(sessionId, expectedVersion), cancellationToken));

    public sealed record CancelBody(string Reason);

    [HttpPost("{sessionId:long}/cancel")]
    public async Task<ActionResult<CancelInventorySessionResponse>> Cancel(
        long sessionId, [FromQuery] int expectedVersion, CancelBody body, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new CancelInventorySessionCommand(sessionId, expectedVersion, body.Reason), cancellationToken));

    public sealed record AdjustBody(string Adjustment, long? IncidentId);

    [HttpPut("{sessionId:long}/records/{recordId:long}/adjustment")]
    public async Task<ActionResult<AdjustInventoryRecordResponse>> Adjust(
        long sessionId, long recordId, [FromQuery] int expectedVersion, AdjustBody body, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new AdjustInventoryRecordCommand(sessionId, recordId, expectedVersion, body.Adjustment, body.IncidentId), cancellationToken));
}
