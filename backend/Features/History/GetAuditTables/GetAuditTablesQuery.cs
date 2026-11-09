using Mediator;

namespace TecnmInventario.Api.Features.History.GetAuditTables;

/// <summary>Tablas que aparecen en la bitácora (para el filtro de la pantalla).</summary>
public sealed record GetAuditTablesQuery() : IQuery<IReadOnlyList<string>>;
