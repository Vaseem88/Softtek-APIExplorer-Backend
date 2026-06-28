using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Softtek_APIExplorer_Backend.Exceptions;
using Softtek_APIExplorer_Backend.Models;

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
        var document = ParseDocument(payload);

        var endpoints = ExtractEndpoints(document);
        if (endpoints.Count == 0)
        {
            throw new AppException("OpenAPI specification contains no paths.", HttpStatusCode.BadRequest);
        }

        var serverUrls = document.Servers
            .Select(s => s.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (serverUrls.Count == 0 && !string.IsNullOrWhiteSpace(request.SwaggerUrl))
        {
            serverUrls.Add(request.SwaggerUrl.Trim());
        }

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

    private static OpenApiDocument ParseDocument(string rawPayload)
    {
        try
        {
            var openApiReader = new OpenApiStringReader();
            var document = openApiReader.Read(rawPayload, out var diagnostic);

            if (diagnostic?.Errors is { Count: > 0 })
            {
                var firstError = diagnostic.Errors[0].Message;
                throw new AppException($"Malformed OpenAPI content. {firstError}", HttpStatusCode.BadRequest);
            }

            return document;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new AppException($"Malformed OpenAPI content. {exception.Message}", HttpStatusCode.BadRequest);
        }
    }

    private static IReadOnlyCollection<OpenApiEndpointMetadata> ExtractEndpoints(OpenApiDocument document)
    {
        var endpoints = new List<OpenApiEndpointMetadata>();

        foreach (var pathItem in document.Paths)
        {
            foreach (var operation in pathItem.Value.Operations)
            {
                var parameters = operation.Value.Parameters
                    .Select(p => $"{p.Name} ({p.In}, required: {p.Required})")
                    .ToList();

                var requestSchemas = operation.Value.RequestBody?.Content
                    .Select(content => DescribeSchema(content.Value.Schema))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                    ?? [];

                var responseSchemas = operation.Value.Responses
                    .SelectMany(response => response.Value.Content)
                    .Select(content => DescribeSchema(content.Value.Schema))
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                endpoints.Add(new OpenApiEndpointMetadata
                {
                    Path = pathItem.Key,
                    Method = operation.Key.ToString().ToUpperInvariant(),
                    Summary = operation.Value.Summary,
                    Description = operation.Value.Description,
                    Parameters = parameters,
                    RequestSchemas = requestSchemas,
                    ResponseSchemas = responseSchemas
                });
            }
        }

        return endpoints;
    }

    private static string? DescribeSchema(OpenApiSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        if (schema.Reference?.Id is not null)
        {
            return schema.Reference.Id;
        }

        return schema.Type;
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
