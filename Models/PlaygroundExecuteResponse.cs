using System.Net;

namespace Softtek_APIExplorer_Backend.Models;

public sealed class PlaygroundExecuteResponse
{
    public required HttpStatusCode StatusCode { get; init; }
    public required string ResponseBody { get; init; }
    public Dictionary<string, string> ResponseHeaders { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? SemanticErrorExplanation { get; init; }
}
