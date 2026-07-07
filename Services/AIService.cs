using Azure;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
// using Microsoft.SemanticKernel.Memory; // Updated using directive for SQLiteVectorStore
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using OpenAI.Chat;
using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services;

public sealed class AIService
{
    private readonly ILogger<AIService> _logger;
    private readonly AzureOpenAIClient _azureClient;
    private readonly string modelId;
    private readonly TextEmbeddingAIService _textEmbeddingAIService;


    public AIService(IConfiguration configuration, ILogger<AIService> logger, TextEmbeddingAIService textEmbeddingAIService)
    {
        _logger = logger;
        _textEmbeddingAIService = textEmbeddingAIService;

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


    public async Task<string> RunKnowledgeBaseAgent(string userInput)
    {
        var vectorStoreCollection = await _textEmbeddingAIService.CreateSQLiteDBConnection();
        // Initialize the ChatClient with the specified deployment name
        Microsoft.Agents.AI.ChatClientAgent chatClient = _azureClient.GetChatClient(modelId).AsAIAgent(
            instructions: "you are a helpful internal knowledge base ai agent who answers API related questions and always use the search_internal_kb tool to fetch your data ",
            tools: [AIFunctionFactory.Create(new SearchTool(vectorStoreCollection).Search, "search_internal_kb")]
            );

        var res = await chatClient.RunAsync(userInput);

        Console.WriteLine(res);

        return res.Text ?? string.Empty;
    }

    public async Task<bool> IngestData(PlaygroundLoadResponse playgroundLoadResponse, CancellationToken cancellationToken = default)
    {
        try
        {
            var vectorStoreCollection = await _textEmbeddingAIService.CreateSQLiteDBConnection();
            return await VectorStoreService.IngestData(vectorStoreCollection, playgroundLoadResponse, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }

    }
}
