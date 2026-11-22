using Mediator;

namespace TecnmInventario.Api.Features.Tickets.GetTicketStats;

/// <summary>
/// Movimiento de tickets en [From, To) (UTC): recibidos, resueltos, cerrados y cancelados en ese lapso, más lo
/// pendiente ahora. Sin tickets.manage cuenta solo los propios (misma regla que GetTickets).
/// </summary>
public sealed record GetTicketStatsQuery(DateTime From, DateTime To) : IQuery<GetTicketStatsResponse>;
