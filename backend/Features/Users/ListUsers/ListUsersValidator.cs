using FluentValidation;

namespace TecnmInventario.Api.Features.Users.ListUsers;

public class ListUsersValidator : AbstractValidator<ListUsersQuery>
{
    private static readonly string[] Statuses = { "activo", "inactivo", "suspendido" };

    public ListUsersValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100).When(x => x.Search is not null);
        RuleFor(x => x.Status)
            .Must(s => Statuses.Contains(s))
            .WithMessage("Estado no válido: activo, inactivo o suspendido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}
