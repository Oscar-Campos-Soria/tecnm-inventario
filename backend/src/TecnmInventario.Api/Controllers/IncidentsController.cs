using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Incidents.CreateIncident;
using TecnmInventario.Api.Features.Incidents.GetIncidentById;
using TecnmInventario.Api.Features.Incidents.GetIncidents;

namespace TecnmInventario.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IncidentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public IncidentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public sealed record IncidentFilters(
        long? AssetId,
        string? Status,
        string? Priority,
        long? IncidentTypeId,
        long? AssignedTo,
        bool? OnlyOpen,
        string? Search);

    [HttpGet]
    [Authorize(Policy = "incidents.view")]
    public async Task<ActionResult<PagedResult<GetIncidentsResponse>>> GetIncidents(
        [FromQuery] PagedRequest pagination,
        [FromQuery] IncidentFilters filters,
        CancellationToken cancellationToken)
    {
        var query = new GetIncidentsQuery(
            pagination.PageNumber, pagination.PageSize,
            filters.AssetId, filters.Status, filters.Priority,
            filters.IncidentTypeId, filters.AssignedTo, filters.OnlyOpen, filters.Search);

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{incidentId:long}")]
    [Authorize(Policy = "incidents.view")]
    public async Task<ActionResult<GetIncidentByIdResponse>> GetIncidentById(
        long incidentId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetIncidentByIdQuery(incidentId), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "incidents.create")]
    public async Task<ActionResult<CreateIncidentResponse>> CreateIncident(
        CreateIncidentCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetIncidentById), new { incidentId = result.IncidentId }, result);
    }
}
