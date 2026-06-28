using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services;

public interface IOpenApiSpecService
{
    Task<PlaygroundLoadResponse> LoadAsync(PlaygroundLoadFormRequest request, CancellationToken cancellationToken);
    OpenApiSessionContext GetRequiredSession(string sessionId);
}
