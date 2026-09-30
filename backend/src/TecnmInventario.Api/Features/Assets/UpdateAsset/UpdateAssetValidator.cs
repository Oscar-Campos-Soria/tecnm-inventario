using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Infrastructure;

namespace TecnmInventario.Api.Features.Assets.UpdateAsset;

public class UpdateAssetValidator : AbstractValidator<UpdateAssetCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public UpdateAssetValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.ManufacturerId)
            .MustAsync(ManufacturerExists).WithMessage("El fabricante especificado no existe.")
            .When(x => x.ManufacturerId.HasValue);

        RuleFor(x => x.ModelId)
            .MustAsync(ModelExists).WithMessage("El modelo especificado no existe.")
            .When(x => x.ModelId.HasValue);

        RuleFor(x => x.CurrentLocationId)
            .MustAsync(LocationExists).WithMessage("La ubicación especificada no existe.")
            .When(x => x.CurrentLocationId.HasValue);

        RuleFor(x => x.SerialNumber).MaximumLength(150);
        RuleFor(x => x.Hostname).MaximumLength(150);
    }

    private async Task<bool> ManufacturerExists(long? id, CancellationToken ct)
        => await _context.Manufacturers.AnyAsync(m => m.ManufacturerId == id && !m.IsDeleted, ct);

    private async Task<bool> ModelExists(long? id, CancellationToken ct)
        => await _context.Models.AnyAsync(m => m.ModelId == id && !m.IsDeleted, ct);

    private async Task<bool> LocationExists(long? id, CancellationToken ct)
        => await _context.Locations.AnyAsync(l => l.LocationId == id && !l.IsDeleted, ct);
}
