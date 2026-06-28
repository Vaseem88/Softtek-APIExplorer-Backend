using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services;

public interface IChatService
{
    Task<PlaygroundChatResponse> ResolveIntentAsync(PlaygroundChatRequest request, OpenApiSessionContext session, CancellationToken cancellationToken);
}
