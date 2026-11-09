using Mediator;
using TecnmInventario.Api.Features.BulkImport.Shared;

namespace TecnmInventario.Api.Features.BulkImport.PreviewImport;

/// <summary>Revisa el archivo fila por fila sin escribir nada.</summary>
public sealed class PreviewImportHandler : IQueryHandler<PreviewImportQuery, PreviewImportResponse>
{
    private readonly AssetImportChecker _checker;

    public PreviewImportHandler(AssetImportChecker checker)
    {
        _checker = checker;
    }

    public async ValueTask<PreviewImportResponse> Handle(PreviewImportQuery query, CancellationToken cancellationToken)
    {
        var rows = await _checker.CheckAsync(AssetImportParser.Parse(query.File), cancellationToken);
        return new PreviewImportResponse(
            rows.Count,
            rows.Count(r => r.IsValid),
            rows.Select(r => new PreviewRow(r.RowNumber, r.InventoryCode, r.TypeName, r.LocationName, r.Errors)).ToList());
    }
}
