using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Features.BulkImport.CommitImport;
using TecnmInventario.Api.Features.BulkImport.GetImportTemplate;
using TecnmInventario.Api.Features.BulkImport.PreviewImport;
using TecnmInventario.Api.Features.BulkImport.Shared;

namespace TecnmInventario.Api.Features.BulkImport;

[ApiController]
[Route("api/bulk-import/assets")]
[Authorize(Policy = "assets.create")]
public class BulkImportController : ControllerBase
{
    private readonly IMediator _mediator;

    public BulkImportController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("template.xlsx")]
    public async Task<IActionResult> Template(CancellationToken cancellationToken)
    {
        var file = await _mediator.Send(new GetImportTemplateQuery(), cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("preview")]
    [RequestSizeLimit(ImportFile.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<PreviewImportResponse>> Preview(IFormFile file, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new PreviewImportQuery(await ReadAsync(file, cancellationToken)), cancellationToken));

    [HttpPost("commit")]
    [RequestSizeLimit(ImportFile.MaxBytes + 64 * 1024)]
    public async Task<ActionResult<CommitImportResponse>> Commit(IFormFile file, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new CommitImportCommand(await ReadAsync(file, cancellationToken)), cancellationToken));

    private static async Task<ImportFile> ReadAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length > ImportFile.MaxBytes)
            throw new BusinessRuleViolationException("El archivo pesa más de 5 MB.");
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        return new ImportFile(file.FileName, stream.ToArray());
    }
}
