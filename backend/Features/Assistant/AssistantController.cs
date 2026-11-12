using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecnmInventario.Api.Features.Assistant.AskAssistant;
using TecnmInventario.Api.Features.Assistant.EndConversation;
using TecnmInventario.Api.Features.Assistant.GetAssistantStatus;

namespace TecnmInventario.Api.Features.Assistant;

[ApiController]
[Route("api/assistant")]
[Authorize(Policy = "assistant.use")]
public class AssistantController : ControllerBase
{
    private readonly IMediator _mediator;

    public AssistantController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("status")]
    public async Task<ActionResult<GetAssistantStatusResponse>> Status(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetAssistantStatusQuery(), cancellationToken));

    [HttpPost("ask")]
    public async Task<ActionResult<AskAssistantResponse>> Ask(AskAssistantCommand command, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(command, cancellationToken));

    [HttpDelete("conversations/{conversationId:guid}")]
    public async Task<IActionResult> EndConversation(Guid conversationId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new EndConversationCommand(conversationId), cancellationToken);
        return NoContent();
    }
}
