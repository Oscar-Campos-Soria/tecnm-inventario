using FluentValidation;
using TecnmInventario.Api.Common.Domain;

namespace TecnmInventario.Api.Features.Users.UpdateUser;

public class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.ExpectedVersion)
            .GreaterThan(0).WithMessage("Debe indicar la versión del usuario que está viendo (expectedVersion).");
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(DbFieldLengths.Users.Email)
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(DbFieldLengths.Users.FirstName);
        RuleFor(x => x.LastName).NotEmpty().WithMessage("El apellido es obligatorio.").MaximumLength(DbFieldLengths.Users.LastName);
        RuleFor(x => x.RoleCode).NotEmpty().WithMessage("El rol es obligatorio.");
    }
}
