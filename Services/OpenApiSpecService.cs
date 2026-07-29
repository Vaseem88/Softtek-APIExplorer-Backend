using Microsoft.Extensions.Caching.Memory;
using Softtek_APIExplorer_Backend.Exceptions;
using Softtek_APIExplorer_Backend.Models;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Softtek_APIExplorer_Backend.Services;

public sealed class OpenApiSpecService : IOpenApiSpecService
{
    private const string CachePrefix = "openapi-session:";

    private readonly IMemoryCache _memoryCache;
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenApiSpecService(IMemoryCache memoryCache, IHttpClientFactory httpClientFactory)
    {
        _memoryCache = memoryCache;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<PlaygroundLoadResponse> LoadAsync(PlaygroundLoadFormRequest request, CancellationToken cancellationToken)
    {
        var payload = await ReadPayloadAsync(request, cancellationToken);
        var document = JsonSerializer.Deserialize<OpenApiSpecifications>(payload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (document is null)
        {
            throw new AppException("Unable to parse OpenAPI specification.", HttpStatusCode.BadRequest);
        }

        var endpoints = ExtractEndpoints(document);
        if (endpoints.Count == 0)
        {
            throw new AppException("OpenAPI specification contains no paths.", HttpStatusCode.BadRequest);
        }

        var serverUrls = ExtractServerUrls(document, request.SwaggerUrl);

        var allowedDomains = serverUrls
            .Select(TryGetHost)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allowedDomains.Count == 0)
        {
            throw new AppException("No valid server domain could be inferred from the specification.", HttpStatusCode.BadRequest);
        }

        var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId.Trim();
        var context = new OpenApiSessionContext
        {
            SessionId = sessionId,
            AllowedDomains = allowedDomains,
            ServerUrls = serverUrls,
            Endpoints = endpoints,
            LoadedAtUtc = DateTimeOffset.UtcNow
        };

        _memoryCache.Set(GetCacheKey(sessionId), context, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(2),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
        });

        return new PlaygroundLoadResponse
        {
            SessionId = context.SessionId,
            EndpointCount = context.Endpoints.Count,
            AllowedDomains = context.AllowedDomains,
            Endpoints = context.Endpoints
        };
    }

    public OpenApiSessionContext GetRequiredSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new AppException("SessionId is required.", HttpStatusCode.BadRequest);
        }

        if (_memoryCache.TryGetValue<OpenApiSessionContext>(GetCacheKey(sessionId), out var session) && session is not null)
        {
            return session;
        }

        throw new AppException("Session not found or expired. Reload an OpenAPI document.", HttpStatusCode.NotFound);
    }

    private async Task<string> ReadPayloadAsync(PlaygroundLoadFormRequest request, CancellationToken cancellationToken)
    {
        var hasUrl = !string.IsNullOrWhiteSpace(request.SwaggerUrl);
        var hasFile = request.OpenApiFile is not null;

        if (hasUrl == hasFile)
        {
            throw new AppException("Provide either swaggerUrl or openApiFile.", HttpStatusCode.BadRequest);
        }

        if (hasUrl)
        {
            if (!Uri.TryCreate(request.SwaggerUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new AppException("Invalid swaggerUrl format.", HttpStatusCode.BadRequest);
            }

            var client = _httpClientFactory.CreateClient("OpenApiSourceClient");
            using var response = await client.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new AppException($"Failed to load specification from URL. Status: {(int)response.StatusCode}", HttpStatusCode.BadRequest);
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        if (request.OpenApiFile is null || request.OpenApiFile.Length == 0)
        {
            throw new AppException("OpenAPI file is empty.", HttpStatusCode.BadRequest);
        }

        await using var stream = request.OpenApiFile.OpenReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, leaveOpen: false);
        return await reader.ReadToEndAsync(cancellationToken);
    }


    private static IReadOnlyCollection<OpenApiEndpointMetadata> ExtractEndpoints(OpenApiSpecifications document)
    {
        var endpoints = new List<OpenApiEndpointMetadata>();

        foreach (var pathItem in document.Paths)
        {
            foreach (var operation in GetOperations(pathItem.Value))
            {
                var parameters = operation.Operation.Parameters
                    .Select(p => BuildParameter(p))
                    .ToList();

                var requestSchemas = operation.Operation.Parameters
                    .Where(p => string.Equals(p.In, "body", StringComparison.OrdinalIgnoreCase))
                    .Select(p => DescribeSchema(document, p.Schema))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var responseSchemas = operation.Operation.Responses
                    .Select(response => DescribeSchema(document, response.Value.Schema))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                endpoints.Add(new OpenApiEndpointMetadata
                {
                    Path = pathItem.Key,
                    Method = operation.Method,
                    Summary = operation.Operation.Summary,
                    Description = operation.Operation.Description,
                    Parameters = parameters,
                    RequestSchemas = requestSchemas,
                    ResponseSchemas = responseSchemas
                });
            }
        }

        return endpoints;
    }

    private static string BuildParameter(OpenApiParameter p)
    {
        StringBuilder result = new StringBuilder($"name: {p.Name} type: {p.Schema?.Type ?? p.Type} (in: {p.In}, required: {p.Required}, description: {p.Description})");
        if(p.Items?.Enum != null && p.Items.Enum.Count > 0) {
            result.Append($" (enum: {string.Join(", ", p.Items.Enum)})");
        }

        return result.ToString();
    }

    private static IReadOnlyCollection<string> ExtractServerUrls(OpenApiSpecifications document, string? swaggerUrl)
    {
        var serverUrls = new List<string>();

        var host = document.Host?.Trim();
        if (!string.IsNullOrWhiteSpace(host))
        {
            var basePath = string.IsNullOrWhiteSpace(document.BasePath)
                ? string.Empty
                : document.BasePath.StartsWith('/') ? document.BasePath : $"/{document.BasePath}";

            var schemes = document.Schemes
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (schemes.Count == 0 && Uri.TryCreate(swaggerUrl, UriKind.Absolute, out var requestUri))
            {
                schemes.Add(requestUri.Scheme);
            }

            foreach (var scheme in schemes)
            {
                serverUrls.Add($"{scheme}://{host}{basePath}");
            }
        }

        if (serverUrls.Count == 0 && !string.IsNullOrWhiteSpace(swaggerUrl))
        {
            serverUrls.Add(swaggerUrl.Trim());
        }

        return serverUrls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyCollection<(string Method, OpenApiOperation Operation)> GetOperations(OpenApiPathItem pathItem)
    {
        var operations = new List<(string Method, OpenApiOperation Operation)>();

        if (pathItem.Get is not null) operations.Add(("GET", pathItem.Get));
        if (pathItem.Put is not null) operations.Add(("PUT", pathItem.Put));
        if (pathItem.Post is not null) operations.Add(("POST", pathItem.Post));
        if (pathItem.Delete is not null) operations.Add(("DELETE", pathItem.Delete));
        if (pathItem.Options is not null) operations.Add(("OPTIONS", pathItem.Options));
        if (pathItem.Head is not null) operations.Add(("HEAD", pathItem.Head));
        if (pathItem.Patch is not null) operations.Add(("PATCH", pathItem.Patch));

        return operations;
    }

    private static string? DescribeSchema(OpenApiSpecifications document, OpenApiSpecificationSchema? schema, ISet<string>? visitedDefinitions = null)
    {
        if (schema is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(schema.Ref))
        {
            var definitionKey = TryGetDefinitionKey(schema.Ref);
            if (!string.IsNullOrWhiteSpace(definitionKey) && document.Definitions.TryGetValue(definitionKey, out var definition))
            {
                visitedDefinitions ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!visitedDefinitions.Add(definitionKey))
                {
                    return definitionKey;
                }

                return DescribeSchema(document, definition, visitedDefinitions);
            }

            return schema.Ref;
        }

        if (string.Equals(schema.Type, "array", StringComparison.OrdinalIgnoreCase))
        {
            var itemDescription = DescribeSchema(document, schema.Items, visitedDefinitions) ?? "object";
            return $"array<{itemDescription}>";
        }

        if (string.Equals(schema.Type, "object", StringComparison.OrdinalIgnoreCase) && schema.Properties.Count > 0)
        {
            var properties = schema.Properties
                .Select(p => FormatPropertySchema(document, visitedDefinitions, p));
            return $"object{{{string.Join(", ", properties)}}}";
        }

        return schema.Type;
    }

    private static string FormatPropertySchema(OpenApiSpecifications document, ISet<string>? visitedDefinitions, KeyValuePair<string, OpenApiSpecificationSchema> p)
    {
        StringBuilder result = new StringBuilder($"{p.Key}: {DescribeSchema(document, p.Value, visitedDefinitions) ?? "object"}");
        if (p.Value.Description != null) { 
            result.Append($" (description: {p.Value.Description})");
        }
        if(p.Value.Enum != null && p.Value.Enum.Count > 0) {
            result.Append($" (enum: {string.Join(", ", p.Value.Enum)})");
        }
        return result.ToString();
    }

    private static string? TryGetDefinitionKey(string reference)
    {
        const string definitionPrefix = "#/definitions/";
        if (!reference.StartsWith(definitionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return reference[definitionPrefix.Length..];
    }

    private static string GetCacheKey(string sessionId) => $"{CachePrefix}{sessionId}";

    private static string? TryGetHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Host;
    }
}
