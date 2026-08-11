using Azure.AI.OpenAI;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using OpenAI.Chat;
using Softtek_APIExplorer_Backend.Models;
using System.Collections.Concurrent;
using System.Text;

namespace Softtek_APIExplorer_Backend.Services;

internal sealed class AIServiceKnowledgeBaseHelper
{
    private const string DefaultKnowledgeBaseInstructions = "you are a helpful internal knowledge base ai agent who answers API related questions and always use the search_internal_kb tool to fetch your data";
    private const string InstructionsCacheKey = "ai:kb:instructions";

    private readonly ILogger<AIService> _logger;
    private readonly AzureOpenAIClient _azureClient;
    private readonly string _modelId;
    private readonly TextEmbeddingAIService _textEmbeddingAIService;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly TimeSpan _instructionsCacheDuration;
    private readonly TimeSpan _responseCacheDuration;
    private readonly Lazy<Task<ChatClientAgent>> _knowledgeBaseChatClient;
    private readonly ConcurrentDictionary<string, Lazy<Task<AgentSession>>> _knowledgeBaseSessions = new(StringComparer.Ordinal);

    public AIServiceKnowledgeBaseHelper(
        ILogger<AIService> logger,
        AzureOpenAIClient azureClient,
        string modelId,
        TextEmbeddingAIService textEmbeddingAIService,
        IConfiguration configuration,
        IMemoryCache memoryCache,
        TimeSpan instructionsCacheDuration,
        TimeSpan responseCacheDuration)
    {
        _logger = logger;
        _azureClient = azureClient;
        _modelId = modelId;
        _textEmbeddingAIService = textEmbeddingAIService;
        _configuration = configuration;
        _memoryCache = memoryCache;
        _instructionsCacheDuration = instructionsCacheDuration;
        _responseCacheDuration = responseCacheDuration;

        _knowledgeBaseChatClient = new Lazy<Task<ChatClientAgent>>(
            CreateKnowledgeBaseChatClientAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<string> ExecuteKnowledgeBaseQueryAsync(string userSessionId, string normalizedInput)
    {
        var responseCacheKey = GetKnowledgeBaseResponseCacheKey(userSessionId, normalizedInput);
        if (_memoryCache.TryGetValue<string>(responseCacheKey, out var cachedResponse) && !string.IsNullOrWhiteSpace(cachedResponse))
        {
            return cachedResponse;
        }

        var chatClient = await _knowledgeBaseChatClient.Value;
        var session = await GetOrCreateSessionAsync(userSessionId);

        var res = await chatClient.RunAsync(normalizedInput, session);
        var responseText = res.Text ?? string.Empty;
        _memoryCache.Set(responseCacheKey, responseText, _responseCacheDuration);

        return responseText;
    }


    public async IAsyncEnumerable<string> ExecuteKnowledgeBaseQueryStreamAsync(string userSessionId, string normalizedInput)
    {
        var responseCacheKey = GetKnowledgeBaseResponseCacheKey(userSessionId, normalizedInput);

        if (_memoryCache.TryGetValue<string>(responseCacheKey, out var cachedResponse) && !string.IsNullOrWhiteSpace(cachedResponse))
        {
            yield return cachedResponse;
            yield break;
        }

        var chatClient = await _knowledgeBaseChatClient.Value;
        var session = await GetOrCreateSessionAsync(userSessionId);

        var stream = chatClient.RunStreamingAsync(normalizedInput, session);
        var fullResponse = new StringBuilder();

        await foreach (var chunk in stream)
        {
            if (chunk.Text is null)
            {
                continue;
            }

            fullResponse.Append(chunk.Text);
            yield return chunk.Text;
        }

        var responseText = fullResponse.ToString();
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            _memoryCache.Set(responseCacheKey, responseText, _responseCacheDuration);
        }
    }

    public bool TryGetCachedResponse(string userSessionId, string userInput, out string response)
    {
        response = string.Empty;
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return false;
        }

        var responseCacheKey = GetKnowledgeBaseResponseCacheKey(userSessionId, userInput.Trim());
        if (_memoryCache.TryGetValue<string>(responseCacheKey, out var cachedResponse) && !string.IsNullOrWhiteSpace(cachedResponse))
        {
            response = cachedResponse;
            return true;
        }

        return false;
    }

    public static string GetKnowledgeBaseResponseCacheKey(string userSessionId, string normalizedInput)
        => $"ai:kb:response:{userSessionId.ToLowerInvariant()}:{normalizedInput.ToLowerInvariant()}";

    private async Task<ChatClientAgent> CreateKnowledgeBaseChatClientAsync()
    {
        var aiSystemInstructions = await GetKnowledgeBaseInstructionsAsync(CancellationToken.None);

        var vectorStoreCollection = await _textEmbeddingAIService.CreateSQLiteCollection();

        return _azureClient.GetChatClient(_modelId).AsAIAgent(
            instructions: aiSystemInstructions,
            tools: [AIFunctionFactory.Create(new SearchTool(vectorStoreCollection).Search, "search_internal_kb")]
            );
    }

    private async Task<AgentSession> GetOrCreateSessionAsync(string userSessionId)
    {
        var lazySession = _knowledgeBaseSessions.GetOrAdd(
            userSessionId,
            _ => new Lazy<Task<AgentSession>>(CreateSessionAsync, LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazySession.Value;
        }
        catch
        {
            _knowledgeBaseSessions.TryRemove(userSessionId, out _);
            throw;
        }
    }

    private async Task<AgentSession> CreateSessionAsync()
    {
        var chatClient = await _knowledgeBaseChatClient.Value;
        return await chatClient.CreateSessionAsync();
    }

    private async Task<string> GetKnowledgeBaseInstructionsAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue<string>(InstructionsCacheKey, out var cachedInstructions) && !string.IsNullOrWhiteSpace(cachedInstructions))
        {
            return cachedInstructions;
        }

        var fileName = _configuration["AI:InstructionsPath"];
        string instructions = DefaultKnowledgeBaseInstructions;

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var filePath = Path.IsPathRooted(fileName)
                ? fileName
                : Path.Combine(Directory.GetCurrentDirectory(), fileName);

            if (File.Exists(filePath))
            {
                instructions = await File.ReadAllTextAsync(filePath, cancellationToken);
            }
            else
            {
                _logger.LogWarning("AI instructions file was not found at path: {FilePath}. Falling back to default instructions.", filePath);
            }
        }

        _memoryCache.Set(InstructionsCacheKey, instructions, _instructionsCacheDuration);
        return instructions;
    }
}
