using Mediator;
using TecnmInventario.Api.Common.Pdf;

namespace TecnmInventario.Api.Features.BulkImport.GetAiJobResult;

/// <summary>Plantilla llena por la IA (solo del usuario que inició la conversión).</summary>
public sealed record GetAiJobResultQuery(Guid JobId) : IQuery<GeneratedFile>;
