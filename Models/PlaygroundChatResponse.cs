namespace Softtek_APIExplorer_Backend.Models;

public sealed class PlaygroundChatResponse
{
    public required string Endpoint { get; init; }
    public required string Explanation { get; init; }
    public required string CurlCommand { get; init; }
    public required string CsharpSnippet { get; init; }
    public required string JavascriptSnippet { get; init; }
    public required IReadOnlyCollection<string> WorkflowSequence { get; init; }
}
