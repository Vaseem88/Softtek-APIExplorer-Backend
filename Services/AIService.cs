using Azure;
using Azure.AI.OpenAI;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
// using Microsoft.SemanticKernel.Memory; // Updated using directive for SQLiteVectorStore
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using OpenAI.Chat;
using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services;

public sealed class AIService
{
    private const string DefaultKnowledgeBaseInstructions = "you are a helpful internal knowledge base ai agent who answers API related questions and always use the search_internal_kb tool to fetch your data";
    private const string InstructionsCacheKey = "ai:kb:instructions";

    private readonly ILogger<AIService> _logger;
    private readonly AzureOpenAIClient _azureClient;
    private readonly string modelId;
    private readonly TextEmbeddingAIService _textEmbeddingAIService;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly TimeSpan _instructionsCacheDuration;
    private readonly TimeSpan _responseCacheDuration;
    private readonly ConcurrentDictionary<string, Task<string>> _inFlightKnowledgeBaseRequests = new(StringComparer.Ordinal);

    public AIService(
        IConfiguration configuration,
        ILogger<AIService> logger,
        TextEmbeddingAIService textEmbeddingAIService,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _textEmbeddingAIService = textEmbeddingAIService;
        _configuration = configuration;
        _memoryCache = memoryCache;

        modelId = configuration["AI:ModelId"];
        var apiKey = configuration["AI:ApiKey"];
        var endpoint = configuration["AI:Endpoint"];

        if (string.IsNullOrWhiteSpace(modelId) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("Missing AI configuration. Set AI:ModelId, AI:ApiKey, and AI:Endpoint.");
        }


        AzureKeyCredential credential = new AzureKeyCredential(apiKey);

        // Initialize the AzureOpenAIClient
        _azureClient = new(new Uri(endpoint), credential);

        _instructionsCacheDuration = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue<int?>("AI:InstructionsCacheMinutes") ?? 30));
        _responseCacheDuration = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue<int?>("AI:KnowledgeBaseResponseCacheSeconds") ?? 60));

    }

    public async Task<string> RunAgentAsync(string userInput, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            throw new ArgumentException("User input is required.", nameof(userInput));
        }
        // Initialize the ChatClient with the specified deployment name
        var chatClient = _azureClient.GetChatClient(modelId).AsAIAgent(
            );

        var res = await chatClient.RunAsync(userInput);

        Console.WriteLine(res);
        return res.Text ?? string.Empty;
    }


    public async Task<string> RunKnowledgeBaseAgent(string userInput, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            throw new ArgumentException("User input is required.", nameof(userInput));
        }

        var normalizedInput = userInput.Trim();
        var responseCacheKey = GetKnowledgeBaseResponseCacheKey(normalizedInput);
        return await ExecuteKnowledgeBaseQueryAsync(normalizedInput, responseCacheKey);
    }

    public bool TryGetKnowledgeBaseCachedResponse(string userInput, out string response)
    {
        response = string.Empty;
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return false;
        }

        var responseCacheKey = GetKnowledgeBaseResponseCacheKey(userInput.Trim());
        if (_memoryCache.TryGetValue<string>(responseCacheKey, out var cachedResponse) && !string.IsNullOrWhiteSpace(cachedResponse))
        {
            response = cachedResponse;
            return true;
        }

        return false;
    }

    public Task WarmKnowledgeBaseCacheAsync(string userInput, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return Task.CompletedTask;
        }

        return Task.Run(async () =>
        {
            try
            {
                _ = await RunKnowledgeBaseAgent(userInput, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to warm knowledge base cache for prompt.");
            }
        }, cancellationToken);
    }

    private async Task<string> ExecuteKnowledgeBaseQueryAsync(string normalizedInput, string responseCacheKey)
    {
        if (_memoryCache.TryGetValue<string>(responseCacheKey, out var cachedResponse) && !string.IsNullOrWhiteSpace(cachedResponse))
        {
            return cachedResponse;
        }

        var aiSystemInstructions = await GetKnowledgeBaseInstructionsAsync(CancellationToken.None);

        var vectorStoreCollection = await _textEmbeddingAIService.CreateSQLiteCollection();

        Microsoft.Agents.AI.ChatClientAgent chatClient = _azureClient.GetChatClient(modelId).AsAIAgent(
            instructions: aiSystemInstructions,
            tools: [AIFunctionFactory.Create(new SearchTool(vectorStoreCollection).Search, "search_internal_kb")]
            );

        var res = await chatClient.RunAsync(normalizedInput);
        var responseText = res.Text ?? string.Empty;
        _memoryCache.Set(responseCacheKey, responseText, _responseCacheDuration);

        return responseText;
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

    private static string GetKnowledgeBaseResponseCacheKey(string normalizedInput)
        => $"ai:kb:response:{normalizedInput.ToLowerInvariant()}";

    public async Task<bool> IngestData(PlaygroundLoadResponse playgroundLoadResponse, CancellationToken cancellationToken = default)
    {
        try
        {
            var vectorStoreCollection = await _textEmbeddingAIService.CreateSQLiteCollection();
            return await VectorStoreService.IngestData(vectorStoreCollection, playgroundLoadResponse, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }

    }
}
