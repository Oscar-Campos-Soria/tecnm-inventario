using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Tickets.AddTicketComment;
using TecnmInventario.Api.Features.Tickets.AssignTicket;
using TecnmInventario.Api.Features.Tickets.CreateTicket;
using TecnmInventario.Api.Features.Tickets.GetTicketById;
using TecnmInventario.Api.Features.Tickets.GetTickets;
using TecnmInventario.Api.Features.Tickets.LinkTicketIncident;
using TecnmInventario.Api.Features.Tickets.ResolveTicket;
using TecnmInventario.Api.Features.Tickets.UpdateTicketPriority;
using TecnmInventario.Api.Features.Tickets.UpdateTicketStatus;

namespace TecnmInventario.Api.Features.Tickets;

/// <summary>
/// Mesa de ayuda. tickets.create alcanza para operar sobre los propios
/// (la visibilidad la decide cada handler); tickets.manage es la bandeja del técnico.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TicketsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public sealed record TicketFilters(
        string? Status,
        long? CategoryId,
        string? Priority,
        long? AssignedTo,
        bool? Unassigned,
        bool? OnlyOpen,
        string? Search);

    [HttpGet]
    [Authorize(Policy = "tickets.create")]
    public async Task<ActionResult<PagedResult<GetTicketsResponse>>> GetTickets(
        [FromQuery] PagedRequest pagination,
        [FromQuery] TicketFilters filters,
        CancellationToken cancellationToken)
    {
        var query = new GetTicketsQuery(
            pagination.PageNumber, pagination.PageSize,
            filters.Status, filters.CategoryId, filters.Priority,
            filters.AssignedTo, filters.Unassigned, filters.OnlyOpen, filters.Search);

        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{ticketId:long}")]
    [Authorize(Policy = "tickets.create")]
    public async Task<ActionResult<GetTicketByIdResponse>> GetTicketById(
        long ticketId,
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetTicketByIdQuery(ticketId), cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "tickets.create")]
    public async Task<ActionResult<CreateTicketResponse>> CreateTicket(
        CreateTicketCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetTicketById), new { ticketId = result.TicketId }, result);
    }

    public sealed record AddTicketCommentBody(string Body, bool IsInternal = false);

    [HttpPost("{ticketId:long}/comments")]
    [Authorize(Policy = "tickets.create")]
    public async Task<ActionResult<AddTicketCommentResponse>> AddComment(
        long ticketId,
        AddTicketCommentBody body,
        CancellationToken cancellationToken)
    {
        var command = new AddTicketCommentCommand(ticketId, body.Body, body.IsInternal);
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    public sealed record AssignTicketBody(long AssignedToUserId);

    [HttpPut("{ticketId:long}/assignment")]
    [Authorize(Policy = "tickets.manage")]
    public async Task<ActionResult<AssignTicketResponse>> AssignTicket(
        long ticketId,
        [FromQuery] int expectedVersion,
        AssignTicketBody body,
        CancellationToken cancellationToken)
    {
        var command = new AssignTicketCommand(ticketId, expectedVersion, body.AssignedToUserId);
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    public sealed record UpdateTicketStatusBody(string NewStatus, string? Reason);

    [HttpPatch("{ticketId:long}/status")]
    [Authorize(Policy = "tickets.create")]
    public async Task<ActionResult<UpdateTicketStatusResponse>> UpdateStatus(
        long ticketId,
        [FromQuery] int expectedVersion,
        UpdateTicketStatusBody body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTicketStatusCommand(ticketId, expectedVersion, body.NewStatus, body.Reason);
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    public sealed record UpdateTicketPriorityBody(long PriorityId);

    [HttpPatch("{ticketId:long}/priority")]
    [Authorize(Policy = "tickets.manage")]
    public async Task<ActionResult<UpdateTicketPriorityResponse>> UpdatePriority(
        long ticketId,
        [FromQuery] int expectedVersion,
        UpdateTicketPriorityBody body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTicketPriorityCommand(ticketId, expectedVersion, body.PriorityId);
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    public sealed record ResolveTicketBody(string Resolution);

    [HttpPost("{ticketId:long}/resolve")]
    [Authorize(Policy = "tickets.manage")]
    public async Task<ActionResult<ResolveTicketResponse>> Resolve(
        long ticketId,
        [FromQuery] int expectedVersion,
        ResolveTicketBody body,
        CancellationToken cancellationToken)
    {
        var command = new ResolveTicketCommand(ticketId, expectedVersion, body.Resolution);
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    public sealed record LinkTicketIncidentBody(long IncidentId);

    [HttpPut("{ticketId:long}/incident")]
    [Authorize(Policy = "tickets.manage")]
    public async Task<ActionResult<LinkTicketIncidentResponse>> LinkIncident(
        long ticketId,
        [FromQuery] int expectedVersion,
        LinkTicketIncidentBody body,
        CancellationToken cancellationToken)
    {
        var command = new LinkTicketIncidentCommand(ticketId, expectedVersion, body.IncidentId);
        return Ok(await _mediator.Send(command, cancellationToken));
    }
}
