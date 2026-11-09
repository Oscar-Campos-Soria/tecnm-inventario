using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Pagination;
using TecnmInventario.Api.Features.Software.Licenses.AssignLicense;
using TecnmInventario.Api.Features.Software.Licenses.CreateLicense;
using TecnmInventario.Api.Features.Software.Licenses.DeleteLicense;
using TecnmInventario.Api.Features.Software.Licenses.EndLicenseAssignment;
using TecnmInventario.Api.Features.Software.Licenses.GetLicenseAssignments;
using TecnmInventario.Api.Features.Software.Licenses.GetLicenseById;
using TecnmInventario.Api.Features.Software.Licenses.GetLicenses;
using TecnmInventario.Api.Features.Software.Licenses.UpdateLicense;

namespace TecnmInventario.Api.Features.Software.Licenses;

[ApiController]
[Route("api/software/licenses")]
[Authorize(Policy = "software.manage")]
public class LicensesController : ControllerBase
{
    private readonly IMediator _mediator;

    public LicensesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<GetLicensesResponse>>> GetLicenses(
        [FromQuery] PagedRequest pagination,
        [FromQuery] long? softwareId,
        [FromQuery] int? expiringInDays,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var query = new GetLicensesQuery(
            pagination.PageNumber, pagination.PageSize, softwareId, expiringInDays, search);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    [HttpGet("{licenseId:long}")]
    public async Task<ActionResult<GetLicenseByIdResponse>> GetLicenseById(
        long licenseId, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetLicenseByIdQuery(licenseId), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CreateLicenseResponse>> CreateLicense(
        CreateLicenseCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetLicenseById), new { licenseId = result.LicenseId }, result);
    }

    public sealed record UpdateLicenseBody(
        long LicenseTypeId,
        string? LicenseKey,
        int? QuantityPurchased,
        DateOnly? ValidFrom,
        DateOnly? ValidUntil,
        long? SupplierId,
        string? Notes);

    [HttpPut("{licenseId:long}")]
    public async Task<ActionResult<UpdateLicenseResponse>> UpdateLicense(
        long licenseId,
        [FromQuery] int expectedVersion,
        UpdateLicenseBody body,
        CancellationToken cancellationToken)
    {
        var command = new UpdateLicenseCommand(
            licenseId, expectedVersion, body.LicenseTypeId, body.LicenseKey, body.QuantityPurchased,
            body.ValidFrom, body.ValidUntil, body.SupplierId, body.Notes);
        return Ok(await _mediator.Send(command, cancellationToken));
    }

    [HttpDelete("{licenseId:long}")]
    public async Task<ActionResult<DeleteLicenseResponse>> DeleteLicense(
        long licenseId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new DeleteLicenseCommand(licenseId, expectedVersion), cancellationToken));

    [HttpGet("{licenseId:long}/assignments")]
    public async Task<ActionResult<PagedResult<GetLicenseAssignmentsResponse>>> GetAssignments(
        long licenseId,
        [FromQuery] PagedRequest pagination,
        [FromQuery] bool onlyActive,
        CancellationToken cancellationToken)
    {
        var query = new GetLicenseAssignmentsQuery(licenseId, pagination.PageNumber, pagination.PageSize, onlyActive);
        return Ok(await _mediator.Send(query, cancellationToken));
    }

    public sealed record AssignLicenseBody(long AssetId);

    [HttpPost("{licenseId:long}/assignments")]
    public async Task<ActionResult<AssignLicenseResponse>> Assign(
        long licenseId, AssignLicenseBody body, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new AssignLicenseCommand(licenseId, body.AssetId), cancellationToken);
        return CreatedAtAction(nameof(GetAssignments), new { licenseId }, result);
    }

    [HttpPost("assignments/{licenseAssignmentId:long}/end")]
    public async Task<ActionResult<EndLicenseAssignmentResponse>> EndAssignment(
        long licenseAssignmentId,
        [FromQuery] int expectedVersion,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new EndLicenseAssignmentCommand(licenseAssignmentId, expectedVersion), cancellationToken));
}
