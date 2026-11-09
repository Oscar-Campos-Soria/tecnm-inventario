using FluentValidation;
using TecnmInventario.Api.Features.Reports.Shared;

namespace TecnmInventario.Api.Features.Reports.GetReport;

public class GetReportValidator : AbstractValidator<GetReportQuery>
{
    public GetReportValidator()
    {
        RuleFor(x => x.Report)
            .Must(r => ReportCodes.All.Contains(r))
            .WithMessage($"Reporte desconocido. Usa uno de: {string.Join(", ", ReportCodes.All)}.");
        RuleFor(x => x.Format)
            .Must(f => f is ReportCodes.Xlsx or ReportCodes.Pdf)
            .WithMessage("El formato debe ser xlsx o pdf.");
        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From)
            .When(x => x.From is not null && x.To is not null)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}
