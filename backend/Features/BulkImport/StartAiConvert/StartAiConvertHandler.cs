using Mediator;
using TecnmInventario.Api.Common.Ai;
using TecnmInventario.Api.Common.Errors;
using TecnmInventario.Api.Common.Security;
using TecnmInventario.Api.Features.BulkImport.Shared.Ai;

namespace TecnmInventario.Api.Features.BulkImport.StartAiConvert;

public sealed class StartAiConvertHandler : ICommandHandler<StartAiConvertCommand, StartAiConvertResponse>
{
    private readonly ILocalAiClient _ai;
    private readonly AiImportJobs _jobs;
    private readonly ICurrentUserService _currentUser;

    public StartAiConvertHandler(ILocalAiClient ai, AiImportJobs jobs, ICurrentUserService currentUser)
    {
        _ai = ai;
        _jobs = jobs;
        _currentUser = currentUser;
    }

    public async ValueTask<StartAiConvertResponse> Handle(StartAiConvertCommand command, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenAccessException("Sesión no válida.");
        if (!await _ai.IsAvailableAsync(cancellationToken))
            throw new BusinessRuleViolationException("El asistente IA no está disponible. Usa la plantilla o pide al administrador que lo encienda.");
        return new StartAiConvertResponse(_jobs.Enqueue(userId, command.File).Id);
    }
}
