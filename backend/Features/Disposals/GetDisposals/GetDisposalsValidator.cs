using FluentValidation;

namespace TecnmInventario.Api.Features.Disposals.GetDisposals;

public class GetDisposalsValidator : AbstractValidator<GetDisposalsQuery>
{
    public GetDisposalsValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100).When(x => x.Search is not null);
    }
}
