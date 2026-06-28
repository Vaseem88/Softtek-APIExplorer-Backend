namespace Softtek_APIExplorer_Backend.Models;

public sealed class PlaygroundChatRequest
{
    public required string SessionId { get; init; }
    public required string Intent { get; init; }
}
