using System.Text;
using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services;

public sealed class SemanticErrorService : ISemanticErrorService
{
    private readonly IEnterpriseLlmClient _llmClient;

    public SemanticErrorService(IEnterpriseLlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    public async Task<string> ExplainAsync(string technicalError, OpenApiSessionContext session, CancellationToken cancellationToken)
    {
        var systemPrompt = BuildSemanticErrorSystemPrompt(session);
        var explanation = await _llmClient.GenerateStructuredAsync<SemanticErrorExplanation>(
            systemPrompt,
            technicalError,
            cancellationToken);

        return $"Root cause: {explanation.RootCause} Suggested fix: {explanation.SuggestedFix}";
    }

    private static string BuildSemanticErrorSystemPrompt(OpenApiSessionContext session)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("You explain failed internal API calls in plain English for enterprise developers.");
        prompt.AppendLine("Do not leak sensitive data. Keep response concise and technical.");
        prompt.AppendLine("Use only this JSON schema:");
        prompt.AppendLine("{ \"rootCause\": \"...\", \"suggestedFix\": \"...\" }");
        prompt.AppendLine($"Known domains: {string.Join(",", session.AllowedDomains)}");
        return prompt.ToString();
    }
}
