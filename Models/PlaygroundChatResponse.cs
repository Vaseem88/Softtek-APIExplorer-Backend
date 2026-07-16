namespace Softtek_APIExplorer_Backend.Models;

public sealed class PlaygroundChatResponse
{
    public string Endpoint { get; init; }
    public string Explanation { get; init; }
    public string CurlCommand { get; init; }
    public string CsharpSnippet { get; init; }
    public string JavascriptSnippet { get; init; }
    public IReadOnlyCollection<string> WorkflowSequence { get; init; }
}
