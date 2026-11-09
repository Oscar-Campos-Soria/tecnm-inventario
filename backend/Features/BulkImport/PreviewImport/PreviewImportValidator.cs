using FluentValidation;
using TecnmInventario.Api.Features.BulkImport.Shared;

namespace TecnmInventario.Api.Features.BulkImport.PreviewImport;

public class PreviewImportValidator : AbstractValidator<PreviewImportQuery>
{
    public PreviewImportValidator()
    {
        RuleFor(x => x.File).SetValidator(new ImportFileValidator());
    }
}
