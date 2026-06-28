using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services;

public sealed class PlaygroundOrchestratorService : IPlaygroundOrchestratorService
{
    private readonly IOpenApiSpecService _openApiSpecService;
    private readonly IChatService _chatService;
    private readonly IExecutionProxyService _executionProxyService;

    public PlaygroundOrchestratorService(
        IOpenApiSpecService openApiSpecService,
        IChatService chatService,
        IExecutionProxyService executionProxyService)
    {
        _openApiSpecService = openApiSpecService;
        _chatService = chatService;
        _executionProxyService = executionProxyService;
    }

    public Task<PlaygroundLoadResponse> LoadOpenApiAsync(PlaygroundLoadFormRequest request, CancellationToken cancellationToken)
    {
        return _openApiSpecService.LoadAsync(request, cancellationToken);
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
