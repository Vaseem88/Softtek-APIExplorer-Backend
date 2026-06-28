using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services;

public interface IExecutionProxyService
{
    Task<PlaygroundExecuteResponse> ExecuteAsync(PlaygroundExecuteRequest request, OpenApiSessionContext session, CancellationToken cancellationToken);
}
