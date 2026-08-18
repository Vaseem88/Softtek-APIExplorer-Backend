using Softtek_APIExplorer_Backend.Models;

using Softtek_APIExplorer_Backend.Exceptions;
using System.Net;

namespace Softtek_APIExplorer_Backend.Services;

public sealed class PlaygroundOrchestratorService : IPlaygroundOrchestratorService
{
    private readonly IOpenApiSpecService _openApiSpecService;
    private readonly IChatService _chatService;
    private readonly IExecutionProxyService _executionProxyService;
    private readonly AIService _aiService;

    public PlaygroundOrchestratorService(
        IOpenApiSpecService openApiSpecService,
        IChatService chatService,
        IExecutionProxyService executionProxyService,
        AIService aiService)
    {
        _openApiSpecService = openApiSpecService;
        _chatService = chatService;
        _executionProxyService = executionProxyService;
        _aiService = aiService;
    }

    public async Task<PlaygroundLoadResponse> LoadOpenApiAsync(PlaygroundLoadFormRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _openApiSpecService.LoadAsync(request, cancellationToken);

            var isDataIngested = await _aiService.IngestData(result);

            if (isDataIngested)
            {
                return result;
            }
            else
            {
                throw new AppException("Failed to ingest data into AI service.", HttpStatusCode.InternalServerError);
            }
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new AppException("Failed to load OpenAPI session.", HttpStatusCode.InternalServerError);
        }
    }

    public async Task<PlaygroundChatResponse> ChatAsync(PlaygroundChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var session = _openApiSpecService.GetRequiredSession(request.SessionId);
            return await _chatService.ResolveIntentAsync(request, session, cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new AppException("Failed to process chat operation.", HttpStatusCode.InternalServerError);
        }
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(PlaygroundChatRequest request, CancellationToken cancellationToken)
    {
        var session = _openApiSpecService.GetRequiredSession(request.SessionId);
        await foreach(var chucks in _chatService.ResolveIntentStreamAsync(request, session, cancellationToken))
        {
            yield return chucks;
        }
    }

    public async Task<PlaygroundExecuteResponse> ExecuteAsync(PlaygroundExecuteRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var session = _openApiSpecService.GetRequiredSession(request.SessionId);
            return await _executionProxyService.ExecuteAsync(request, session, cancellationToken);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new AppException("Failed to execute proxy operation.", HttpStatusCode.InternalServerError);
        }
    }
}
