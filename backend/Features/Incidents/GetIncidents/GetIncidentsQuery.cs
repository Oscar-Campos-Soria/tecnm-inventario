using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Incidents.GetIncidents;

public sealed record GetIncidentsQuery(
    int PageNumber,
    int PageSize,
    long? AssetId,
    string? StatusCode,
    string? PriorityCode,
    long? IncidentTypeId,
    long? AssignedTo,
    bool? OnlyOpen,
    string? Search,
    // Reportados desde esta fecha (UTC). Lo usa el asistente para "¿qué hay de nuevo?".
    DateTime? ReportedFrom = null,
    // Incidentes de activos de este tipo (ej. todas las impresoras). Lo usa el asistente.
    long? AssetTypeId = null) : IQuery<PagedResult<GetIncidentsResponse>>;
