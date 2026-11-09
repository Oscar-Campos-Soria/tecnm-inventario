using FluentValidation;

namespace TecnmInventario.Api.Features.Profile.ChangePassword;

public class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Escribe tu contraseña actual.");

        // Misma regla que el registro y el restablecimiento por el administrador.
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("La contraseña nueva es obligatoria.")
            .MinimumLength(8).WithMessage("La contraseña nueva debe tener al menos 8 caracteres.")
            .MaximumLength(128)
            .NotEqual(x => x.CurrentPassword).WithMessage("La contraseña nueva debe ser distinta de la actual.");
    }
}
