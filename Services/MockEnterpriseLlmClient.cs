using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services;

public sealed class MockEnterpriseLlmClient : IEnterpriseLlmClient
{
    public Task<T> GenerateStructuredAsync<T>(string systemPrompt, string userPrompt, CancellationToken cancellationToken) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (typeof(T) == typeof(PlaygroundChatResponse))
        {
            var endpoint = InferEndpointFromPrompt(systemPrompt, userPrompt);
            var response = new PlaygroundChatResponse
            {
                Endpoint = endpoint,
                Explanation = "The selected endpoint best matches the user intent based on the loaded OpenAPI metadata.",
                CurlCommand = BuildCurl(endpoint),
                CsharpSnippet = BuildCsharpSnippet(endpoint),
                JavascriptSnippet = BuildJavascriptSnippet(endpoint),
                WorkflowSequence = [
                    "Step 1: Acquire corporate access token",
                    $"Step 2: Invoke {endpoint}"
                ]
            };

            return Task.FromResult((T)(object)response);
        }

        if (typeof(T) == typeof(SemanticErrorExplanation))
        {
            var explanation = new SemanticErrorExplanation
            {
                RootCause = $"Downstream API returned an error payload: {Trim(userPrompt, 180)}",
                SuggestedFix = "Validate required fields, authentication headers, and request schema against the OpenAPI definition."
            };

            return Task.FromResult((T)(object)explanation);
        }

        throw new NotSupportedException($"Mock LLM does not support type {typeof(T).Name}.");
    }

    private static string InferEndpointFromPrompt(string systemPrompt, string userPrompt)
    {
        var methodTokens = new[] { "GET", "POST", "PUT", "PATCH", "DELETE" };
        var lines = systemPrompt.Split('"');

        var firstEndpoint = lines.FirstOrDefault(line => methodTokens.Any(m => line.StartsWith(m, StringComparison.OrdinalIgnoreCase)));
        if (!string.IsNullOrWhiteSpace(firstEndpoint))
        {
            return firstEndpoint;
        }

        var inferredMethod = userPrompt.Contains("create", StringComparison.OrdinalIgnoreCase) ? "POST" : "GET";
        return $"{inferredMethod} /api/v1/resource";
    }

    private static string BuildCurl(string endpoint)
    {
        var parts = endpoint.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var method = parts.Length > 0 ? parts[0] : "GET";
        var path = parts.Length > 1 ? parts[1] : "/api/v1/resource";
        return $"curl -X {method} \"https://internal.company.local{path}\" -H \"Authorization: Bearer <token>\" -H \"Content-Type: application/json\"";
    }

    private static string BuildCsharpSnippet(string endpoint)
    {
        var parts = endpoint.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var method = parts.Length > 0 ? parts[0] : "GET";
        var path = parts.Length > 1 ? parts[1] : "/api/v1/resource";

        return $"var request = new HttpRequestMessage(HttpMethod.{NormalizeMethod(method)}, \"{path}\");\nrequest.Headers.Authorization = new AuthenticationHeaderValue(\"Bearer\", token);\nvar response = await httpClient.SendAsync(request, cancellationToken);\nlogger.LogInformation(\"Executed {method} {path} with status {{StatusCode}}\", (int)response.StatusCode);";
    }

    private static string BuildJavascriptSnippet(string endpoint)
    {
        var parts = endpoint.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var method = parts.Length > 0 ? parts[0] : "GET";
        var path = parts.Length > 1 ? parts[1] : "/api/v1/resource";

        return $"const response = await fetch('{path}', {{ method: '{method}', headers: {{ Authorization: 'Bearer <token>', 'Content-Type': 'application/json' }} }});\nconst data = await response.json();";
    }

    private static string NormalizeMethod(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => "Get",
            "POST" => "Post",
            "PUT" => "Put",
            "PATCH" => "Patch",
            "DELETE" => "Delete",
            _ => "Get"
        };
    }

    private static string Trim(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }
}
