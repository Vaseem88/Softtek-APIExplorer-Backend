namespace Softtek_APIExplorer_Backend.Models;

public sealed class PlaygroundLoadResponse
{
    public required string SessionId { get; init; }
    public required int EndpointCount { get; init; }
    public required IReadOnlyCollection<string> AllowedDomains { get; init; }
    public required IReadOnlyCollection<OpenApiEndpointMetadata> Endpoints { get; init; }
}
