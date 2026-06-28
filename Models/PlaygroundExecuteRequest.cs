using System.Text.Json;

namespace Softtek_APIExplorer_Backend.Models;

public sealed class PlaygroundExecuteRequest
{
    public required string SessionId { get; init; }
    public required string Method { get; init; }
    public required string Path { get; init; }
    public string? Url { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public Dictionary<string, string>? QueryParameters { get; init; }
    public JsonElement? Body { get; init; }
}
