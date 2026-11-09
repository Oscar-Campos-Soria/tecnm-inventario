using FluentValidation;

namespace TecnmInventario.Api.Features.Users.ChangeUserStatus;

public class ChangeUserStatusValidator : AbstractValidator<ChangeUserStatusCommand>
{
    private static readonly string[] Statuses = { "activo", "inactivo", "suspendido" };

    public ChangeUserStatusValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del usuario que está viendo (expectedVersion).");
        RuleFor(x => x.Status)
            .Must(s => Statuses.Contains(s))
            .WithMessage("Estado no válido: activo, inactivo o suspendido.");
    }
}
