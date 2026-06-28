namespace Softtek_APIExplorer_Backend.Models;

public sealed class OpenApiEndpointMetadata
{
    public required string Path { get; init; }
    public required string Method { get; init; }
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public IReadOnlyCollection<string> Parameters { get; init; } = [];
    public IReadOnlyCollection<string> RequestSchemas { get; init; } = [];
    public IReadOnlyCollection<string> ResponseSchemas { get; init; } = [];
}
