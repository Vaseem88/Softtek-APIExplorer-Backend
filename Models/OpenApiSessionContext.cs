namespace Softtek_APIExplorer_Backend.Models;

public sealed class OpenApiSessionContext
{
    public required string SessionId { get; init; }
    public required IReadOnlyCollection<string> AllowedDomains { get; init; }
    public required IReadOnlyCollection<string> ServerUrls { get; init; }
    public required IReadOnlyCollection<OpenApiEndpointMetadata> Endpoints { get; init; }
    public required DateTimeOffset LoadedAtUtc { get; init; }
}
