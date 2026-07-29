using System.Text.Json;
using System.Text.Json.Serialization;

namespace Softtek_APIExplorer_Backend.Models;

public sealed class OpenApiSpecifications
{
    public string? Swagger { get; init; }
    public OpenApiInfo? Info { get; init; }
    public string? Host { get; init; }
    public string? BasePath { get; init; }
    public IReadOnlyCollection<OpenApiTag> Tags { get; init; } = [];
    public IReadOnlyCollection<string> Schemes { get; init; } = [];
    public IReadOnlyDictionary<string, OpenApiPathItem> Paths { get; init; } = new Dictionary<string, OpenApiPathItem>();
    public IReadOnlyDictionary<string, OpenApiSecurityDefinition> SecurityDefinitions { get; init; } = new Dictionary<string, OpenApiSecurityDefinition>();
    public IReadOnlyDictionary<string, OpenApiSpecificationSchema> Definitions { get; init; } = new Dictionary<string, OpenApiSpecificationSchema>();
    public OpenApiExternalDocs? ExternalDocs { get; init; }
}

public sealed class OpenApiInfo
{
    public string? Description { get; init; }
    public string? Version { get; init; }
    public string? Title { get; init; }
    public string? TermsOfService { get; init; }
    public OpenApiContact? Contact { get; init; }
    public OpenApiLicense? License { get; init; }
}

public sealed class OpenApiContact
{
    public string? Email { get; init; }
}

public sealed class OpenApiLicense
{
    public string? Name { get; init; }
    public string? Url { get; init; }
}

public sealed class OpenApiTag
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public OpenApiExternalDocs? ExternalDocs { get; init; }
}

public sealed class OpenApiExternalDocs
{
    public string? Description { get; init; }
    public string? Url { get; init; }
}

public sealed class OpenApiPathItem
{
    public OpenApiOperation? Get { get; init; }
    public OpenApiOperation? Put { get; init; }
    public OpenApiOperation? Post { get; init; }
    public OpenApiOperation? Delete { get; init; }
    public OpenApiOperation? Options { get; init; }
    public OpenApiOperation? Head { get; init; }
    public OpenApiOperation? Patch { get; init; }
}

public sealed class OpenApiOperation
{
    public IReadOnlyCollection<string> Tags { get; init; } = [];
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public string? OperationId { get; init; }
    public IReadOnlyCollection<string> Consumes { get; init; } = [];
    public IReadOnlyCollection<string> Produces { get; init; } = [];
    public IReadOnlyCollection<OpenApiParameter> Parameters { get; init; } = [];
    public IReadOnlyDictionary<string, OpenApiResponse> Responses { get; init; } = new Dictionary<string, OpenApiResponse>();
    public IReadOnlyCollection<IReadOnlyDictionary<string, IReadOnlyCollection<string>>> Security { get; init; } = [];
    public bool? Deprecated { get; init; }
}

public sealed class OpenApiParameter
{
    public string? Name { get; init; }

    [JsonPropertyName("in")]
    public string? In { get; init; }

    public string? Description { get; init; }
    public bool? Required { get; init; }
    public string? Type { get; init; }
    public string? Format { get; init; }
    public OpenApiSpecificationSchema? Schema { get; init; }
    public OpenApiSpecificationSchema? Items { get; init; }
    public string? CollectionFormat { get; init; }
    public decimal? Maximum { get; init; }
    public decimal? Minimum { get; init; }
}

public sealed class OpenApiResponse
{
    public string? Description { get; init; }
    public OpenApiSpecificationSchema? Schema { get; init; }
    public IReadOnlyDictionary<string, OpenApiHeader> Headers { get; init; } = new Dictionary<string, OpenApiHeader>();
}

public sealed class OpenApiHeader
{
    public string? Type { get; init; }
    public string? Format { get; init; }
    public string? Description { get; init; }
}

public sealed class OpenApiSecurityDefinition
{
    public string? Type { get; init; }
    public string? Name { get; init; }

    [JsonPropertyName("in")]
    public string? In { get; init; }

    public string? AuthorizationUrl { get; init; }
    public string? Flow { get; init; }
    public IReadOnlyDictionary<string, string> Scopes { get; init; } = new Dictionary<string, string>();
}

public sealed class OpenApiSpecificationSchema
{
    [JsonPropertyName("$ref")]
    public string? Ref { get; init; }

    public string? Type { get; init; }
    public string? Format { get; init; }
    public string? Description { get; init; }
    public OpenApiXml? Xml { get; init; }
    public IReadOnlyCollection<string> Enum { get; init; } = [];

    [JsonPropertyName("required")]
    public IReadOnlyCollection<string> RequiredPropertyNames { get; init; } = [];

    public OpenApiSpecificationSchema? Items { get; init; }
    public IReadOnlyDictionary<string, OpenApiSpecificationSchema> Properties { get; init; } = new Dictionary<string, OpenApiSpecificationSchema>();
    public JsonElement? AdditionalProperties { get; init; }
    public JsonElement? Default { get; init; }
}

public sealed class OpenApiXml
{
    public string? Name { get; init; }
    public bool? Wrapped { get; init; }
}
