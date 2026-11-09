using Mediator;

namespace TecnmInventario.Api.Features.Users.GetUsers;

/// <param name="RoleCodes">Filtro opcional por rol, p. ej. ["ADMIN","TECNICO"] para el selector de técnicos.</param>
public sealed record GetUsersQuery(string? Search, IReadOnlyList<string>? RoleCodes = null)
    : IQuery<IReadOnlyList<GetUsersResponse>>;
