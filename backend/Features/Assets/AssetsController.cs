using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Assets.CreateAsset;
using TecnmInventario.Api.Features.Assets.GetAssetById;
using TecnmInventario.Api.Features.Assets.GetAssets;
using TecnmInventario.Api.Features.Assets.UpdateAsset;
using TecnmInventario.Api.Features.Assets.UpdateAssetStatus;
using TecnmInventario.Api.Features.Procurement.Warranties.GetWarrantiesByAsset;
using TecnmInventario.Api.Features.Incidents.GetIncidents;

namespace TecnmInventario.Api.Features.Assets;

[ApiController]
[Route("api/[controller]")]
public class AssetsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AssetsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = "assets.view")]
    public async Task<ActionResult<PagedResult<GetAssetsResponse>>> GetAssets(
        [FromQuery] PagedRequest pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetAssetsQuery(pagination.PageNumber, pagination.PageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{assetId:long}")]
    [Authorize(Policy = "assets.view")]
    public async Task<ActionResult<GetAssetByIdResponse>> GetAssetById(
        long assetId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAssetByIdQuery(assetId), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "assets.create")]
    public async Task<ActionResult<CreateAssetResponse>> CreateAsset(
        CreateAssetCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetAssetById), new { assetId = result.AssetId }, result);
    }

    public sealed record UpdateAssetBody(
        long? ManufacturerId, long? ModelId, long? CurrentLocationId,
        string? SerialNumber, string? Hostname, string? Notes);

    [HttpPut("{assetId:long}")]
    [Authorize(Policy = "assets.update")]
    public async Task<ActionResult<UpdateAssetResponse>> UpdateAsset(
        long assetId,
        [FromQuery] int expectedVersion,
        UpdateAssetBody body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAssetCommand(
            assetId, expectedVersion, body.ManufacturerId, body.ModelId,
            body.CurrentLocationId, body.SerialNumber, body.Hostname, body.Notes);

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    public sealed record UpdateAssetStatusBody(string NewStatusCode, string Reason);

    [HttpPatch("{assetId:long}/status")]
    [Authorize(Policy = "assets.update")]
    public async Task<ActionResult<UpdateAssetStatusResponse>> UpdateAssetStatus(
        long assetId,
        [FromQuery] int expectedVersion,
        UpdateAssetStatusBody body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAssetStatusCommand(assetId, expectedVersion, body.NewStatusCode, body.Reason);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{assetId:long}/warranties")]
    [Authorize(Policy = "procurement.manage")]
    public async Task<ActionResult<IReadOnlyList<GetWarrantiesByAssetResponse>>> GetWarrantiesByAsset(
        long assetId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetWarrantiesByAssetQuery(assetId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{assetId:long}/incidents")]
    [Authorize(Policy = "incidents.view")]
    public async Task<ActionResult<PagedResult<GetIncidentsResponse>>> GetIncidentsByAsset(
        long assetId,
        [FromQuery] PagedRequest pagination,
        CancellationToken cancellationToken)
    {
        var query = new GetIncidentsQuery(
            pagination.PageNumber, pagination.PageSize,
            AssetId: assetId, StatusCode: null, PriorityCode: null,
            IncidentTypeId: null, AssignedTo: null, OnlyOpen: null, Search: null);

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
