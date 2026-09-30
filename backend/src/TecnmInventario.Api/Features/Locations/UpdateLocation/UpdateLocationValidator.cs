using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Infrastructure;

namespace TecnmInventario.Api.Features.Locations.UpdateLocation;

public class UpdateLocationValidator : AbstractValidator<UpdateLocationCommand>
{
    private readonly TecnmInventarioDbContext _context;

    public UpdateLocationValidator(TecnmInventarioDbContext context)
    {
        _context = context;

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la ubicación es obligatorio.")
            .MaximumLength(150);

        RuleFor(x => x.LocationTypeId)
            .MustAsync(LocationTypeExists).WithMessage("El tipo de ubicación especificado no existe.");

        RuleFor(x => x.ParentLocationId)
            .NotEqual(x => x.LocationId)
            .WithMessage("Una ubicación no puede ser su propio padre.")
            .When(x => x.ParentLocationId.HasValue);

        RuleFor(x => x)
            .MustAsync(NotCreateCycle)
            .WithMessage("No se puede asignar como padre a una de sus propias sub-ubicaciones (crearía un ciclo).")
            .WithName("ParentLocationId")
            .When(x => x.ParentLocationId.HasValue);
    }

    private async Task<bool> LocationTypeExists(long id, CancellationToken ct)
        => await _context.LocationTypes.AnyAsync(t => t.LocationTypeId == id && !t.IsDeleted, ct);

    // Sube desde el ParentLocationId propuesto hacia la raíz: si en algún
    // punto se encuentra la propia ubicación que se está editando, el nuevo
    // padre es en realidad un descendiente suyo -> crearía un ciclo.
    private async Task<bool> NotCreateCycle(UpdateLocationCommand command, CancellationToken ct)
    {
        var currentId = command.ParentLocationId;
        var guard = 0;

        while (currentId.HasValue && guard++ < 20)
        {
            if (currentId == command.LocationId)
            {
                return false;
            }

            currentId = await _context.Locations
                .Where(l => l.LocationId == currentId)
                .Select(l => l.ParentLocationId)
                .FirstOrDefaultAsync(ct);
        }

        return true;
    }
}
