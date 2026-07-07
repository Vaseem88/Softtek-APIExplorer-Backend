using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services
{
    public class TextEmbeddingAIService
    {
        private readonly ILogger<TextEmbeddingAIService> _logger;
        private readonly AzureOpenAIClient _embeddingClient;
        private readonly string modelId;

        public TextEmbeddingAIService(IConfiguration configuration, ILogger<TextEmbeddingAIService> logger)
        {
            _logger = logger;

            modelId = configuration["TextEmbeddingAI:ModelId"];
            var apiKey = configuration["TextEmbeddingAI:ApiKey"];
            var endpoint = configuration["TextEmbeddingAI:Endpoint"];

            if (string.IsNullOrWhiteSpace(modelId) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint))
            {
                throw new InvalidOperationException("Missing AI configuration. Set TextEmbeddingAI:ModelId, TextEmbeddingAI:ApiKey, and TextEmbeddingAI:Endpoint.");
            }


            AzureKeyCredential credential = new AzureKeyCredential(apiKey);

            // Initialize the AzureOpenAIClient
            _embeddingClient = new(new Uri(endpoint), credential);

        }


        public async Task<VectorStoreCollection<Guid, ApiQueriesVectorStore>> CreateSQLiteDBConnection()
        {
            string connectionString = @"Data Source=C:\Users\505073150\Desktop\softtek\API-Explorer\Softtek-APIExplorer-Backend\\APIExplorer.db;";
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("Connection string is required.", nameof(connectionString));
            }

            // create a vector store for the SQLite database
            var vectorStore = new SqliteVectorStore(connectionString, new SqliteVectorStoreOptions
            {
                EmbeddingGenerator = _embeddingClient.GetEmbeddingClient(modelId).AsIEmbeddingGenerator()
            });


            VectorStoreCollection<Guid, ApiQueriesVectorStore> vectorStoreCollection = vectorStore.GetCollection<Guid, ApiQueriesVectorStore>("knowledge_base");

            return vectorStoreCollection;
        }
    }
}
