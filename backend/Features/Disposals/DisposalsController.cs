using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Disposals.CreateDisposal;
using TecnmInventario.Api.Features.Disposals.GetDisposals;

namespace TecnmInventario.Api.Features.Disposals;

/// <summary>Baja formal de activos (specs/disposal.spec.md).</summary>
[ApiController]
[Route("api/[controller]")]
public class DisposalsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DisposalsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public sealed record DisposalFilters(long? AssetId, long? DisposalReasonId, string? Search);

    [HttpGet]
    [Authorize(Policy = "assets.view")]
    public async Task<ActionResult<PagedResult<GetDisposalsResponse>>> GetDisposals(
        [FromQuery] PagedRequest pagination,
        [FromQuery] DisposalFilters filters,
        CancellationToken cancellationToken)
    {
        var query = new GetDisposalsQuery(
            pagination.PageNumber, pagination.PageSize, filters.AssetId, filters.DisposalReasonId, filters.Search);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    public sealed record CreateDisposalBody(
        long AssetId, long DisposalReasonId, string Folio, string? Dictamen, DateOnly? DisposedAt);

    [HttpPost]
    [Authorize(Policy = "assets.delete")]
    public async Task<ActionResult<CreateDisposalResponse>> CreateDisposal(
        [FromQuery] int expectedVersion,
        CreateDisposalBody body,
        CancellationToken cancellationToken)
    {
        var command = new CreateDisposalCommand(
            body.AssetId, expectedVersion, body.DisposalReasonId, body.Folio, body.Dictamen, body.DisposedAt);
        var result = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
