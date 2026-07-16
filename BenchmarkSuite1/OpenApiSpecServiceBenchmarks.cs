using System.Text;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Softtek_APIExplorer_Backend.Models;
using Softtek_APIExplorer_Backend.Services;
using Microsoft.VSDiagnostics;

namespace Softtek_APIExplorer_Backend.Benchmarks;
[CPUUsageDiagnoser]
public class OpenApiSpecServiceBenchmarks
{
    private OpenApiSpecService _service = default !;
    private PlaygroundLoadFormRequest _request = default !;
    private const string SampleOpenApi = """
{
  "openapi": "3.0.0",
  "info": { "title": "Sample API", "version": "1.0" },
  "servers": [
    { "url": "https://api.example.com" }
  ],
  "paths": {
    "/orders": {
      "get": {
        "summary": "Get orders",
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": { "$ref": "#/components/schemas/Order" }
                }
              }
            }
          }
        }
      }
    },
    "/orders/{id}": {
      "get": {
        "summary": "Get order by id",
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": { "type": "string" }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "application/json": {
                "schema": { "$ref": "#/components/schemas/Order" }
              }
            }
          }
        }
      }
    }
  },
  "components": {
    "schemas": {
      "Order": {
        "type": "object",
        "properties": {
          "id": { "type": "string" },
          "status": { "type": "string" }
        }
      }
    }
  }
}
""";
    [GlobalSetup]
    public void Setup()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        _service = new OpenApiSpecService(memoryCache, new NoopHttpClientFactory());
        var bytes = Encoding.UTF8.GetBytes(SampleOpenApi);
        var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "openApiFile", "openapi.json");
        _request = new PlaygroundLoadFormRequest
        {
            OpenApiFile = file,
            SessionId = "benchmark-session"
        };
    }

    [Benchmark]
    public async Task<PlaygroundLoadResponse> LoadOpenApiFromFile()
    {
        return await _service.LoadAsync(_request, CancellationToken.None);
    }

    private sealed class NoopHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}