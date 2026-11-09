using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TecnmInventario.Api.Common.Domain;
using TecnmInventario.Api.Data;

namespace TecnmInventario.Api.Features.Software.Installations.InstallSoftware;

public class InstallSoftwareValidator : AbstractValidator<InstallSoftwareCommand>
{
    public InstallSoftwareValidator(TecnmInventarioDbContext context)
    {
        RuleFor(x => x.SoftwareId)
            .MustAsync((id, ct) => context.Softwares.AnyAsync(s => s.SoftwareId == id, ct))
            .WithMessage("El programa no existe.");

        RuleFor(x => x.VersionInstalled).MaximumLength(DbFieldLengths.AssetSoftware.VersionInstalled);

        RuleFor(x => x.InstalledAt)
            .Must(d => d!.Value <= DateTime.UtcNow.AddDays(1))
            .When(x => x.InstalledAt is not null)
            .WithMessage("La fecha de instalación no puede estar en el futuro.");
    }
}
