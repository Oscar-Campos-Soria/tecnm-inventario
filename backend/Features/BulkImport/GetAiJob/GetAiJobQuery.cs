using Mediator;
using TecnmInventario.Api.Features.BulkImport.Shared.Ai;

namespace TecnmInventario.Api.Features.BulkImport.GetAiJob;

/// <summary>Estado de una conversión con IA (solo del usuario que la inició).</summary>
public sealed record GetAiJobQuery(Guid JobId) : IQuery<AiJobSnapshot>;
