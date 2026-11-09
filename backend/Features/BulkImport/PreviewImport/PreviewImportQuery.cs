using Mediator;
using TecnmInventario.Api.Features.BulkImport.Shared;

namespace TecnmInventario.Api.Features.BulkImport.PreviewImport;

public sealed record PreviewImportQuery(ImportFile File) : IQuery<PreviewImportResponse>;

public sealed record PreviewRow(int RowNumber, string? InventoryCode, string? TypeName, string? LocationName, IReadOnlyList<string> Errors);

public sealed record PreviewImportResponse(int TotalRows, int ValidRows, IReadOnlyList<PreviewRow> Rows);
