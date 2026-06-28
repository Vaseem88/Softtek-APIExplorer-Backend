using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services;

public interface ISemanticErrorService
{
    Task<string> ExplainAsync(string technicalError, OpenApiSessionContext session, CancellationToken cancellationToken);
}
