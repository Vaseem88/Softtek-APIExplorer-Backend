using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Softtek_APIExplorer_Backend.Services;
using Microsoft.VSDiagnostics;

namespace Softtek_APIExplorer_Backend.Benchmarks;
[CPUUsageDiagnoser]
public class TextEmbeddingConnectionBenchmarks
{
    private TextEmbeddingAIService _service = default !;
    private string _tempDbPath = string.Empty;
    [GlobalSetup]
    public void Setup()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"kb-benchmark-{Guid.NewGuid():N}.db");
        var settings = new Dictionary<string, string?>
        {
            ["TextEmbeddingAI:ModelId"] = "benchmark-embedding-model",
            ["TextEmbeddingAI:ApiKey"] = "benchmark-key",
            ["TextEmbeddingAI:Endpoint"] = "https://localhost",
            ["TextEmbeddingAI:DatabasePath"] = _tempDbPath
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        _service = new TextEmbeddingAIService(config, NullLogger<TextEmbeddingAIService>.Instance);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_tempDbPath))
        {
            File.Delete(_tempDbPath);
        }
    }

    [Benchmark]
    public async Task CreateSQLiteConnection()
    {
        _ = await _service.CreateSQLiteCollection();
    }
}