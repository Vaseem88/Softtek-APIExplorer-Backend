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
        [FromBody] PlaygroundLoadFormRequest request,
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

    [HttpPost("chatStream")]
    [Produces("text/event-stream")]
    public async Task<IActionResult> ChatStreamAsync(
        [FromBody] PlaygroundChatRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.Intent))
        {
            return BadRequest("SessionId and Intent are required.");
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        await foreach (var chunk in _orchestrator.ChatStreamAsync(request, cancellationToken).WithCancellation(cancellationToken))
        {
            if (string.IsNullOrEmpty(chunk))
            {
                continue;
            }

            await WriteSseEventAsync(chunk, cancellationToken);
        }

        return new EmptyResult();
    }

    private async Task WriteSseEventAsync(string chunk, CancellationToken cancellationToken)
    {
        var normalizedChunk = chunk.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        var lines = SplitLines(normalizedChunk);

        foreach (var line in lines)
        {
            await Response.WriteAsync($"data: {line}\n", cancellationToken);
        }

        await Response.WriteAsync("\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private static List<string> SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var onlyLineBreaks = true;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
            {
                onlyLineBreaks = false;
                break;
            }
        }

        if (onlyLineBreaks)
        {
            var emptyLines = new List<string>(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                emptyLines.Add(string.Empty);
            }

            return emptyLines;
        }

        return text.Split('\n').ToList();
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
