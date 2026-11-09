namespace TecnmInventario.Api.Features.Procurement.Suppliers.GetSuppliers;

public sealed record GetSuppliersResponse(
    long SupplierId,
    string Name,
    string? TaxId,
    string? ContactName,
    string? Email,
    string? Phone,
    // Necesarios para editar sin borrar dirección ni notas (UpdateSupplier reemplaza todo).
    string? Address,
    string? Notes,
    int Version);
