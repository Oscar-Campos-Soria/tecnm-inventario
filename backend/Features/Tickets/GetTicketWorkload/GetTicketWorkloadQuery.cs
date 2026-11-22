using Mediator;

namespace TecnmInventario.Api.Features.Tickets.GetTicketWorkload;

/// <summary>Carga de trabajo pendiente por persona asignada (tickets e incidentes abiertos). Requiere tickets.manage.</summary>
public sealed record GetTicketWorkloadQuery : IQuery<GetTicketWorkloadResponse>;
