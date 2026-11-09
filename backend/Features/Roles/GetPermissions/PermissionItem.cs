namespace TecnmInventario.Api.Features.Roles.GetPermissions;

/// <param name="Module">Prefijo del código (<c>tickets</c> en <c>tickets.manage</c>) para agrupar en pantalla.</param>
public sealed record PermissionItem(string Code, string Name, string? Description, string Module);
