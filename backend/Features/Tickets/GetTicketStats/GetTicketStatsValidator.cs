using FluentValidation;

namespace TecnmInventario.Api.Features.Tickets.GetTicketStats;

public class GetTicketStatsValidator : AbstractValidator<GetTicketStatsQuery>
{
    public GetTicketStatsValidator()
    {
        RuleFor(x => x.To).GreaterThan(x => x.From).WithMessage("La fecha final debe ser posterior a la inicial.");
        RuleFor(x => x).Must(x => x.To - x.From <= TimeSpan.FromDays(366))
            .WithMessage("El periodo no puede pasar de un año.");
    }
}
