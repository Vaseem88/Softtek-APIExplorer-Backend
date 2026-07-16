using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Softtek_APIExplorer_Backend.Services;
using Microsoft.VSDiagnostics;

namespace Softtek_APIExplorer_Backend.Benchmarks;
[CPUUsageDiagnoser]
public class AIServiceKnowledgeBaseCacheBenchmarks
{
    private AIService _aiService = default !;
    private const string Prompt = "How do I call the orders API?";
    [GlobalSetup]
    public void Setup()
    {
        var settings = new Dictionary<string, string?>
        {
            ["AI:ModelId"] = "benchmark-model",
            ["AI:ApiKey"] = "benchmark-key",
            ["AI:Endpoint"] = "https://localhost",
            ["TextEmbeddingAI:ModelId"] = "benchmark-embedding-model",
            ["TextEmbeddingAI:ApiKey"] = "benchmark-key",
            ["TextEmbeddingAI:Endpoint"] = "https://localhost",
            ["TextEmbeddingAI:DatabasePath"] = "bench-kb.db"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var normalizedInput = Prompt.Trim().ToLowerInvariant();
        memoryCache.Set($"ai:kb:response:{normalizedInput}", "Cached benchmark response", TimeSpan.FromMinutes(10));
        var embeddingService = new TextEmbeddingAIService(config, NullLogger<TextEmbeddingAIService>.Instance);
        _aiService = new AIService(config, NullLogger<AIService>.Instance, embeddingService, memoryCache);
    }

    [Benchmark]
    public async Task<string> RunKnowledgeBaseAgent_CacheHit()
    {
        return await _aiService.RunKnowledgeBaseAgent(Prompt, CancellationToken.None);
    }
}