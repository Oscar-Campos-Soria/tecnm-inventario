using Mediator;
using TecnmInventario.Api.Features.BulkImport.Shared;

namespace TecnmInventario.Api.Features.BulkImport.StartAiConvert;

/// <summary>Forma un archivo para que la IA lo convierta a la plantilla. No escribe en la BD.</summary>
public sealed record StartAiConvertCommand(ImportFile File) : ICommand<StartAiConvertResponse>;

public sealed record StartAiConvertResponse(Guid JobId);
