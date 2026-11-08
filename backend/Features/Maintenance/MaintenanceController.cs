using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Maintenance.CreateMaintenance;
using TecnmInventario.Api.Features.Maintenance.DeleteMaintenance;
using TecnmInventario.Api.Features.Maintenance.GetMaintenance;
using TecnmInventario.Api.Features.Maintenance.GetUpcomingMaintenance;

namespace TecnmInventario.Api.Features.Maintenance;

/// <summary>Bitácora de mantenimiento y calendario de preventivos (specs/maintenance.spec.md).</summary>
[ApiController]
[Route("api/maintenance")]
[Authorize(Policy = "maintenance.manage")]
public class MaintenanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public MaintenanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<MaintenanceItem>>> GetMaintenance(
        [FromQuery] PagedRequest pagination,
        [FromQuery] long? assetId,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        var query = new GetMaintenanceQuery(pagination.PageNumber, pagination.PageSize, assetId, type);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("upcoming")]
    public async Task<ActionResult<IReadOnlyList<MaintenanceItem>>> GetUpcoming(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _mediator.Send(new GetUpcomingMaintenanceQuery(days), cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<CreateMaintenanceResponse>> CreateMaintenance(
        CreateMaintenanceCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("{maintenanceRecordId:long}")]
    public async Task<ActionResult<DeleteMaintenanceResponse>> DeleteMaintenance(
        long maintenanceRecordId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new DeleteMaintenanceCommand(maintenanceRecordId, expectedVersion), cancellationToken));
    }
}
