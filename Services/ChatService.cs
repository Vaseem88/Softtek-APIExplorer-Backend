using System.Text;
using System.Text.Json;
using Softtek_APIExplorer_Backend.Exceptions;
using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services;

public sealed class ChatService : IChatService
{
    private readonly IEnterpriseLlmClient _llmClient;
    private readonly AIService _aiService;

    public ChatService(IEnterpriseLlmClient llmClient, AIService aIService)
    {
        _llmClient = llmClient;
        _aiService = aIService;
    }

    public async Task<PlaygroundChatResponse> ResolveIntentAsync(
        PlaygroundChatRequest request,
        OpenApiSessionContext session,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Intent))
        {
            throw new AppException("Intent is required.", System.Net.HttpStatusCode.BadRequest);
        }

        var result = await _aiService.RunKnowledgeBaseAgent(request.Intent);

        //var systemPrompt = BuildSystemPrompt(session);
        //var response = await _llmClient.GenerateStructuredAsync<PlaygroundChatResponse>(
        //    systemPrompt,
        //    request.Intent,
        //    cancellationToken);

        return new PlaygroundChatResponse
        {
            Explanation = result
        };
    }

    private static string BuildSystemPrompt(OpenApiSessionContext session)
    {
        var endpointContext = session.Endpoints
            .Select(e => new
            {
                endpoint = $"{e.Method} {e.Path}",
                e.Summary,
                e.Description,
                e.Parameters,
                e.RequestSchemas,
                e.ResponseSchemas
            })
            .ToList();

        var serializedContext = JsonSerializer.Serialize(endpointContext, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        var prompt = new StringBuilder();
        prompt.AppendLine("You are an enterprise internal API assistant.");
        prompt.AppendLine("Hard guardrails:");
        prompt.AppendLine("1) Suggest only endpoints that exist in the provided OpenAPI context.");
        prompt.AppendLine("2) Never suggest external or unauthorized third-party libraries.");
        prompt.AppendLine("3) Never invent non-existent C# APIs or methods.");
        prompt.AppendLine("4) Keep code snippets aligned to internal enterprise standards: clear naming, async usage, and HttpClient patterns.");
        prompt.AppendLine("5) Use the JSON schema exactly, with no extra fields.");
        prompt.AppendLine("Output must be valid minified JSON only.");
        prompt.AppendLine("JSON schema:");
        prompt.AppendLine("{");
        prompt.AppendLine("  \"endpoint\": \"POST /api/v1/orders\",");
        prompt.AppendLine("  \"explanation\": \"Plain English explanation mapping the business intent to this endpoint.\",");
        prompt.AppendLine("  \"curlCommand\": \"curl -X POST ...\",");
        prompt.AppendLine("  \"csharpSnippet\": \"HttpClient code snippet matching internal enterprise logging and naming standards.\",");
        prompt.AppendLine("  \"javascriptSnippet\": \"fetch() code snippet for the frontend team.\",");
        prompt.AppendLine("  \"workflowSequence\": [\"Step 1: POST /auth\", \"Step 2: POST /orders\"]");
        prompt.AppendLine("}");
        prompt.AppendLine("OpenAPI structural context:");
        prompt.Append(serializedContext);

        return prompt.ToString();
    }
}
