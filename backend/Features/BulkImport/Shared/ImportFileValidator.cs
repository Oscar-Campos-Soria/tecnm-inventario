using FluentValidation;

namespace TecnmInventario.Api.Features.BulkImport.Shared;

public class ImportFileValidator : AbstractValidator<ImportFile>
{
    public ImportFileValidator()
    {
        RuleFor(f => f.Content).Must(c => c.Length > 0).WithMessage("El archivo está vacío.")
            .Must(c => c.Length <= ImportFile.MaxBytes).WithMessage("El archivo pesa más de 5 MB.");
        RuleFor(f => f.Extension).Must(e => e is ".xlsx" or ".csv").WithMessage("Sube un archivo .xlsx o .csv.");
    }
}
