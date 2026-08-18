using Azure;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using OpenAI.Chat;
using Softtek_APIExplorer_Backend.Exceptions;
using Softtek_APIExplorer_Backend.Models;
using System.Net;

namespace Softtek_APIExplorer_Backend.Services;

public sealed class AIService
{
    private const string DefaultUserSessionId = "anonymous";

    private readonly ILogger<AIService> _logger;
    private readonly AzureOpenAIClient _azureClient;
    private readonly string modelId;
    private readonly TextEmbeddingAIService _textEmbeddingAIService;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache;
    private readonly TimeSpan _instructionsCacheDuration;
    private readonly TimeSpan _responseCacheDuration;
    private readonly Microsoft.Agents.AI.ChatClientAgent _defaultChatClient;
    private readonly AIServiceKnowledgeBaseHelper _knowledgeBaseHelper;

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

        _defaultChatClient = _azureClient.GetChatClient(modelId).AsAIAgent();
        _knowledgeBaseHelper = new AIServiceKnowledgeBaseHelper(
            _logger,
            _azureClient,
            modelId,
            _textEmbeddingAIService,
            _configuration,
            _memoryCache,
            _instructionsCacheDuration,
            _responseCacheDuration);

    }

    public async Task<string> RunAgentAsync(string userInput, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                throw new AppException("User input is required.", HttpStatusCode.BadRequest);
            }
            var res = await _defaultChatClient.RunAsync(userInput);

            Console.WriteLine(res);
            return res.Text ?? string.Empty;
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new AppException("Failed to execute AI agent request.", HttpStatusCode.InternalServerError);
        }
    }


    public Task<string> RunKnowledgeBaseAgent(string userInput, CancellationToken cancellationToken = default)
        => RunKnowledgeBaseAgent(DefaultUserSessionId, userInput, cancellationToken);

    public async Task<string> RunKnowledgeBaseAgent(string userSessionId, string userInput, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userSessionId))
            {
                throw new AppException("User session id is required.", HttpStatusCode.BadRequest);
            }

            if (string.IsNullOrWhiteSpace(userInput))
            {
                throw new AppException("User input is required.", HttpStatusCode.BadRequest);
            }

            var normalizedSessionId = userSessionId.Trim();
            var normalizedInput = userInput.Trim();
            return await _knowledgeBaseHelper.ExecuteKnowledgeBaseQueryAsync(normalizedSessionId, normalizedInput);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new AppException("Failed to execute knowledge base request.", HttpStatusCode.InternalServerError);
        }
    }
    public async IAsyncEnumerable<string> RunKnowledgeBaseStreamAgent(string userSessionId, string userInput, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userSessionId))
        {
            throw new ArgumentException("User session id is required.", nameof(userSessionId));
        }

        if (string.IsNullOrWhiteSpace(userInput))
        {
            throw new ArgumentException("User input is required.", nameof(userInput));
        }

        var normalizedSessionId = userSessionId.Trim();
        var normalizedInput = userInput.Trim();
        await foreach(var chunks in _knowledgeBaseHelper.ExecuteKnowledgeBaseQueryStreamAsync(normalizedSessionId, normalizedInput))
        {
            yield return chunks;
        }
    }


    public bool TryGetKnowledgeBaseCachedResponse(string userInput, out string response)
    {
        return _knowledgeBaseHelper.TryGetCachedResponse(DefaultUserSessionId, userInput, out response);
    }

    public Task WarmKnowledgeBaseCacheAsync(string userInput, CancellationToken cancellationToken = default)
        => WarmKnowledgeBaseCacheAsync(DefaultUserSessionId, userInput, cancellationToken);

    public async Task WarmKnowledgeBaseCacheAsync(string userSessionId, string userInput, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userSessionId) || string.IsNullOrWhiteSpace(userInput))
        {
            return;
        }

        try
        {
            _ = await RunKnowledgeBaseAgent(userSessionId, userInput, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to warm knowledge base cache for prompt.");
        }
    }

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
