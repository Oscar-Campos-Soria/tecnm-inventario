using Mediator;
using TecnmInventario.Api.Common.Pdf;

namespace TecnmInventario.Api.Features.Reports.GetReport;

/// <param name="Report">Uno de ReportCodes.All.</param>
/// <param name="Format">xlsx o pdf.</param>
/// <param name="Status">Reporte de activos: código de estado (incluye DADO_DE_BAJA si se pide).</param>
/// <param name="From">Reporte de mantenimientos: desde (fecha de realización).</param>
public sealed record GetReportQuery(
    string Report, string Format, string? Status, long? AssetTypeId, DateOnly? From, DateOnly? To)
    : IQuery<GeneratedFile>;
