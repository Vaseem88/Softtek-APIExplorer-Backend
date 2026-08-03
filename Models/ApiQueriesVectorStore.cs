using Microsoft.Agents.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.OpenApi.Services;
using System.ComponentModel;
using System.Text;

namespace Softtek_APIExplorer_Backend.Models
{
    public class ApiQueriesVectorStore
    {
        [VectorStoreKey]
        public Guid Id { get; set; }
        
        [VectorStoreData]
        public  string Method { get; init; }
        
        [VectorStoreData]
        public string? BaseUrl { get; init; }

        [VectorStoreData]
        public string? Summary { get; init; }

        [VectorStoreData]
        public string? Description { get; init; }

        [VectorStoreData]
        public string Parameters { get; init; }

        [VectorStoreData]
        public string RequestSchemas { get; init; } 

        [VectorStoreData]
        public string ResponseSchemas { get; init; } 

        [VectorStoreData]
        public string Endpoint { get; set; }

        [VectorStoreData]
        public string Product { get; set; }

        [VectorStoreVector(1536)]
        public string Vector => $"Endpoint: {Method} {BaseUrl}/{Endpoint}. Description: {Description}, Summary: {Summary} Parameters: {Parameters}. RequestSchemas: {RequestSchemas}. ResponseSchemas: {ResponseSchemas} ";

    }

    public class SearchTool(VectorStoreCollection<Guid, ApiQueriesVectorStore> vectorStore)
    {
        private const int DefaultNumberOfSearchResults = 2;
        private const int MinNumberOfSearchResults = 1;
        private const int MaxNumberOfSearchResults = 10;

        public async Task<string> Search(
            [Description("The natural language query to search in the internal API knowledge base.")] string input,
            [Description("Optional number of results to retrieve. The value is clamped between 1 and 8.")] int? topK = null)
        {
            StringBuilder mostSimilarknowledge = new StringBuilder();
            int numberOfSearchResults = Math.Clamp(topK ?? DefaultNumberOfSearchResults, MinNumberOfSearchResults, MaxNumberOfSearchResults);
            Console.WriteLine();
            Console.WriteLine($"input: {input}");
            Console.WriteLine($"topK: {numberOfSearchResults}");
            Console.WriteLine("-----------------");

            await foreach (VectorSearchResult<ApiQueriesVectorStore> searchResult in vectorStore.SearchAsync(searchValue:input, top: numberOfSearchResults))
            {
                string result = BuildSearchResultText(searchResult);
                mostSimilarknowledge.Append(result);
                Console.WriteLine();
                Console.WriteLine(result);
            }
            Console.WriteLine("-----------------");
            return mostSimilarknowledge.ToString();
        }

        private static string BuildSearchResultText(VectorSearchResult<ApiQueriesVectorStore> searchResult)
        {
            StringBuilder result = new StringBuilder();

            if (!string.IsNullOrEmpty(searchResult.Record.Endpoint))
            {
                result.Append($"Endpoint: {searchResult.Record.Method} {searchResult.Record.BaseUrl}/{searchResult.Record.Endpoint}.");
            }
            else
            {
                result.Append($"BaseUrl: {searchResult.Record.BaseUrl} Resource: {searchResult.Record.Product} ");
            }

            if (!string.IsNullOrEmpty(searchResult.Record.Description))
            {
                result.Append($" Description: {searchResult.Record.Description}");
            }
            if (!string.IsNullOrEmpty(searchResult.Record.Summary))
            {
                result.Append($" Summary: {searchResult.Record.Summary}");
            }
            if (!string.IsNullOrEmpty(searchResult.Record.Parameters))
            {
                result.Append($" Parameters: {searchResult.Record.Parameters}.");
            }
            if (!string.IsNullOrEmpty(searchResult.Record.RequestSchemas))
            {
                result.Append($" RequestSchemas: {searchResult.Record.RequestSchemas}.");
            }
            if (!string.IsNullOrEmpty(searchResult.Record.ResponseSchemas))
            {
                result.Append($" ResponseSchemas: {searchResult.Record.ResponseSchemas}.");
            }
            return result.ToString();
        }

        public  async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchAdapter(string query, CancellationToken cancellationToken)
        {
            // The mock search inspects the user's question and returns pre-defined snippets
            // that resemble documents stored in an external knowledge source.
            List<TextSearchProvider.TextSearchResult> results = new();
            Console.WriteLine();
            Console.WriteLine($"input: {query}");
            Console.WriteLine("-----------------");

            await foreach (VectorSearchResult<ApiQueriesVectorStore> searchResult in vectorStore.SearchAsync(searchValue: query, top: 3))
            {
                string result = $"Endpoint: {searchResult.Record.Method} {searchResult.Record.BaseUrl}/{searchResult.Record.Endpoint}. Description: {searchResult.Record.Description} Parameters: {searchResult.Record.Parameters}. RequestSchemas: {searchResult.Record.RequestSchemas}. ResponseSchemas: {searchResult.Record.ResponseSchemas} ,";
                Console.WriteLine(result);
                results.Add(new()
                {
                    Text = result
                });
            }
            Console.WriteLine("-----------------");

            return results;
        }

    }
}
