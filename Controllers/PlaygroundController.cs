using Microsoft.AspNetCore.Mvc;
using Softtek_APIExplorer_Backend.Models;
using Softtek_APIExplorer_Backend.Services;

namespace Softtek_APIExplorer_Backend.Controllers;

[ApiController]
[Route("api/playground")]
public sealed class PlaygroundController : ControllerBase
{
    private readonly IPlaygroundOrchestratorService _orchestrator;

    public PlaygroundController(IPlaygroundOrchestratorService orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpPost("load")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<PlaygroundLoadResponse>> LoadOpenApiAsync(
        [FromForm] PlaygroundLoadFormRequest request,
        CancellationToken cancellationToken)
    {
        request.SessionId = Guid.NewGuid().ToString();
        var result = await _orchestrator.LoadOpenApiAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("chat")]
    public async Task<ActionResult<PlaygroundChatResponse>> ChatAsync(
        [FromBody] PlaygroundChatRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _orchestrator.ChatAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteAsync(
        [FromBody] PlaygroundExecuteRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _orchestrator.ExecuteAsync(request, cancellationToken);
        return StatusCode((int)result.StatusCode, result);
    }
}
