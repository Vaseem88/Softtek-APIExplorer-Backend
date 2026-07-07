using Softtek_APIExplorer_Backend.Models;

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
        var result = await _openApiSpecService.LoadAsync(request, cancellationToken);

        var isDataIngested = await _aiService.IngestData(result);

        if (isDataIngested)
        {
            return result;
        }
        else
        {
            throw new Exception("Failed to ingest data into AI service.");
        }
    }

    public async Task<PlaygroundChatResponse> ChatAsync(PlaygroundChatRequest request, CancellationToken cancellationToken)
    {
        var session = _openApiSpecService.GetRequiredSession(request.SessionId);
        return await _chatService.ResolveIntentAsync(request, session, cancellationToken);
    }

    public async Task<PlaygroundExecuteResponse> ExecuteAsync(PlaygroundExecuteRequest request, CancellationToken cancellationToken)
    {
        var session = _openApiSpecService.GetRequiredSession(request.SessionId);
        return await _executionProxyService.ExecuteAsync(request, session, cancellationToken);
    }
}
