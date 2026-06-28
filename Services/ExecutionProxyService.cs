using System.Net;
using System.Text;
using Softtek_APIExplorer_Backend.Exceptions;
using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services;

public sealed class ExecutionProxyService : IExecutionProxyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISemanticErrorService _semanticErrorService;

    public ExecutionProxyService(IHttpClientFactory httpClientFactory, ISemanticErrorService semanticErrorService)
    {
        _httpClientFactory = httpClientFactory;
        _semanticErrorService = semanticErrorService;
    }

    public async Task<PlaygroundExecuteResponse> ExecuteAsync(
        PlaygroundExecuteRequest request,
        OpenApiSessionContext session,
        CancellationToken cancellationToken)
    {



        if (string.IsNullOrWhiteSpace(request.Method))
        {
            throw new AppException("Method is required.", HttpStatusCode.BadRequest);
        }

        var targetUri = BuildTargetUri(request, session);
        ValidateTargetHost(targetUri, session);
        ValidatePathAndMethod(request, targetUri, session);

        using var message = new HttpRequestMessage(new HttpMethod(request.Method.ToUpperInvariant()), targetUri);

        if (request.Headers is not null)
        {
            foreach (var header in request.Headers)
            {
                _ = message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (request.Body is not null && request.Body.Value.ValueKind != System.Text.Json.JsonValueKind.Undefined)
        {
            message.Content = new StringContent(request.Body.Value.GetRawText(), Encoding.UTF8, "application/json");
        }

        var client = _httpClientFactory.CreateClient("PlaygroundProxyClient");
        using var response = await client.SendAsync(message, cancellationToken);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseHeaders = response.Headers
            .Concat(response.Content.Headers)
            .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);

        string? semanticError = null;
        if ((int)response.StatusCode >= 400)
        {
            semanticError = await _semanticErrorService.ExplainAsync(responseBody, session, cancellationToken);
        }

        return new PlaygroundExecuteResponse
        {
            StatusCode = response.StatusCode,
            ResponseBody = responseBody,
            ResponseHeaders = responseHeaders,
            SemanticErrorExplanation = semanticError
        };
    }

    private static Uri BuildTargetUri(PlaygroundExecuteRequest request, OpenApiSessionContext session)
    {
        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var absoluteUri))
            {
                throw new AppException("Invalid absolute URL.", HttpStatusCode.BadRequest);
            }

            return AppendQueryString(absoluteUri, request.QueryParameters);
        }

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            throw new AppException("Path is required when Url is not provided.", HttpStatusCode.BadRequest);
        }

        var serverUrl = session.ServerUrls.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(serverUrl) || !Uri.TryCreate(serverUrl, UriKind.Absolute, out var baseUri))
        {
            throw new AppException("No valid base server URL in active OpenAPI session.", HttpStatusCode.BadRequest);
        }

        var combined = new Uri(baseUri, request.Path);
        return AppendQueryString(combined, request.QueryParameters);
    }

    private static Uri AppendQueryString(Uri uri, Dictionary<string, string>? queryParameters)
    {
        if (queryParameters is null || queryParameters.Count == 0)
        {
            return uri;
        }

        var query = string.Join("&", queryParameters.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        var builder = new UriBuilder(uri)
        {
            Query = query
        };

        return builder.Uri;
    }

    private static void ValidateTargetHost(Uri targetUri, OpenApiSessionContext session)
    {
        var isAllowed = session.AllowedDomains.Contains(targetUri.Host, StringComparer.OrdinalIgnoreCase);
        if (!isAllowed)
        {
            throw new AppException("Target URL host is not part of the loaded OpenAPI servers.", HttpStatusCode.Forbidden);
        }
    }

    private static void ValidatePathAndMethod(PlaygroundExecuteRequest request, Uri targetUri, OpenApiSessionContext session)
    {
        var method = request.Method.ToUpperInvariant();
        var absolutePath = targetUri.AbsolutePath;

        var matches = session.Endpoints.Any(endpoint =>
            endpoint.Method.Equals(method, StringComparison.OrdinalIgnoreCase) &&
            IsPathMatch(endpoint.Path, request.Path));

        if (!matches)
        {
            throw new AppException("Requested method/path does not exist in the loaded OpenAPI specification.", HttpStatusCode.Forbidden);
        }
    }

    private static bool IsPathMatch(string templatePath, string actualPath)
    {
        var templateSegments = templatePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var actualSegments = actualPath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (templateSegments.Length != actualSegments.Length)
        {
            return false;
        }

        for (var index = 0; index < templateSegments.Length; index++)
        {
            var templateSegment = templateSegments[index];
            var actualSegment = actualSegments[index];

            var isParameterSegment = templateSegment.StartsWith('{') && templateSegment.EndsWith('}');
            if (isParameterSegment)
            {
                continue;
            }

            if (!templateSegment.Equals(actualSegment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
