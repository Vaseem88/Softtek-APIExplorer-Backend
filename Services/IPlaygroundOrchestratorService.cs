using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services;

public interface IPlaygroundOrchestratorService
{
    Task<PlaygroundLoadResponse> LoadOpenApiAsync(PlaygroundLoadFormRequest request, CancellationToken cancellationToken);
    Task<PlaygroundChatResponse> ChatAsync(PlaygroundChatRequest request, CancellationToken cancellationToken);
    Task<PlaygroundExecuteResponse> ExecuteAsync(PlaygroundExecuteRequest request, CancellationToken cancellationToken);
}
