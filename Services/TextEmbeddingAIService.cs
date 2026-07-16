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
        private readonly string sqliteConnectionString;
        private readonly SqliteVectorStore _sqliteVectorStore;
        private readonly SemaphoreSlim _collectionLock = new(1, 1);
        private static VectorStoreCollection<Guid, ApiQueriesVectorStore>? _cachedVectorStoreCollection;

        public TextEmbeddingAIService(IConfiguration configuration, ILogger<TextEmbeddingAIService> logger)
        {
            _logger = logger;

            modelId = configuration["TextEmbeddingAI:ModelId"];
            var apiKey = configuration["TextEmbeddingAI:ApiKey"];
            var endpoint = configuration["TextEmbeddingAI:Endpoint"];
            var databasePath = configuration["TextEmbeddingAI:DatabasePath"];

            if (string.IsNullOrWhiteSpace(modelId) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(databasePath))
            {
                throw new InvalidOperationException("Missing AI configuration. Set TextEmbeddingAI:ModelId, TextEmbeddingAI:ApiKey, TextEmbeddingAI:Endpoint, and TextEmbeddingAI:DatabasePath.");
            }


            AzureKeyCredential credential = new AzureKeyCredential(apiKey);

            // Initialize the AzureOpenAIClient
            _embeddingClient = new(new Uri(endpoint), credential);

            var fullDatabasePath = Path.IsPathRooted(databasePath)
                ? databasePath
                : Path.Combine(Directory.GetCurrentDirectory(), databasePath);

            sqliteConnectionString = $"Data Source={fullDatabasePath};";

            if (string.IsNullOrWhiteSpace(sqliteConnectionString))
            {
                throw new ArgumentException("Connection string is required.", nameof(sqliteConnectionString));
            }

            // create a vector store for the SQLite database
            _sqliteVectorStore = new SqliteVectorStore(sqliteConnectionString, new SqliteVectorStoreOptions
            {
                EmbeddingGenerator = _embeddingClient.GetEmbeddingClient(modelId).AsIEmbeddingGenerator()
            });

        }


        public async Task<VectorStoreCollection<Guid, ApiQueriesVectorStore>> CreateSQLiteCollection(string collection = "knowledge_base")
        {
            if (_cachedVectorStoreCollection is not null)
            {
                return _cachedVectorStoreCollection;
            }

            await _collectionLock.WaitAsync();
            try
            {
                if (_cachedVectorStoreCollection is not null)
                {
                    return _cachedVectorStoreCollection;
                }

                VectorStoreCollection<Guid, ApiQueriesVectorStore> vectorStoreCollection = _sqliteVectorStore.GetCollection<Guid, ApiQueriesVectorStore>(collection);
                _cachedVectorStoreCollection = vectorStoreCollection;
                return _cachedVectorStoreCollection;
            }
            finally
            {
                _collectionLock.Release();
            }
        }
    }
}
