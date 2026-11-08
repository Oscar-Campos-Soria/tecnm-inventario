using Mediator;
using TecnmInventario.Api.Common.Pagination;

namespace TecnmInventario.Api.Features.Tickets.GetTickets;

public sealed record GetTicketsQuery(
    int PageNumber,
    int PageSize,
    string? Status,
    long? CategoryId,
    string? PriorityCode,
    long? AssignedTo,
    bool? Unassigned,
    bool? OnlyOpen,
    string? Search) : IQuery<PagedResult<GetTicketsResponse>>;
