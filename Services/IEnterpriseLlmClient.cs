namespace Softtek_APIExplorer_Backend.Services;

public interface IEnterpriseLlmClient
{
    Task<T> GenerateStructuredAsync<T>(string systemPrompt, string userPrompt, CancellationToken cancellationToken) where T : class;
}
