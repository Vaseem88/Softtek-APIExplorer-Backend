namespace Softtek_APIExplorer_Backend.Models;

public sealed class SemanticErrorExplanation
{
    public required string RootCause { get; init; }
    public required string SuggestedFix { get; init; }
}
