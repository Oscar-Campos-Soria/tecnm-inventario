using Mediator;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Infrastructure;
using TecnmInventario.Infrastructure.Entities;

namespace TecnmInventario.Api.Features.Procurement.Acquisitions.CreateAcquisition;

public sealed class CreateAcquisitionHandler : ICommandHandler<CreateAcquisitionCommand, CreateAcquisitionResponse>
{
    private readonly TecnmInventarioDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateAcquisitionHandler(TecnmInventarioDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async ValueTask<CreateAcquisitionResponse> Handle(
        CreateAcquisitionCommand command, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var acquisition = new Acquisition
        {
            SupplierId = command.SupplierId,
            PurchaseOrder = NullIfBlank(command.PurchaseOrder),
            InvoiceNumber = NullIfBlank(command.InvoiceNumber),
            AcquiredAt = command.AcquiredAt,
            Subtotal = command.Subtotal,
            TaxAmount = command.TaxAmount,
            TotalAmount = command.TotalAmount,
            CurrencyCode = command.CurrencyCode.ToUpperInvariant(),
            WarrantyStart = command.WarrantyStart,
            WarrantyEnd = command.WarrantyEnd,
            Notes = command.Notes,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _currentUser.UserId,
            IsDeleted = false,
            Version = 1
        };

        foreach (var assetInput in command.Assets)
        {
            acquisition.AcquisitionAssets.Add(new AcquisitionAsset
            {
                AssetId = assetInput.AssetId,
                UnitCost = assetInput.UnitCost
            });
        }

        // Un solo SaveChangesAsync: EF Core inserta el padre (Acquisition) y
        // los hijos (AcquisitionAssets) en la misma transacción implícita.
        // No hace falta BeginTransactionAsync explícito aquí porque no hay
        // un segundo paso separado (como el INSERT en audit_log que sí
        // requiere SoftDeleteAuditor).
        _context.Acquisitions.Add(acquisition);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateAcquisitionResponse(
            acquisition.AcquisitionId, acquisition.AcquisitionAssets.Count, acquisition.Version);
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
