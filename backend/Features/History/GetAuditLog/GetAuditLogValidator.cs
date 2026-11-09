using FluentValidation;

namespace TecnmInventario.Api.Features.History.GetAuditLog;

public class GetAuditLogValidator : AbstractValidator<GetAuditLogQuery>
{
    public GetAuditLogValidator()
    {
        RuleFor(x => x.TableName).MaximumLength(120).When(x => x.TableName is not null);
        RuleFor(x => x.To)
            .Must((q, to) => to!.Value >= q.From!.Value)
            .WithMessage("La fecha final debe ser posterior a la inicial.")
            .When(x => x.From.HasValue && x.To.HasValue);
    }
}
