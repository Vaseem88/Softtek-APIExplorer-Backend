using Microsoft.Extensions.VectorData;
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
        public string Vector => $"{Method}:{Endpoint}";

    }

    public class SearchTool(VectorStoreCollection<Guid, ApiQueriesVectorStore> vectorStore)
    {
        public async Task<string> Search(string input)
        {
            StringBuilder mostSimilarknowledge = new StringBuilder();
            int numberOfSearchResults = 3;

            await foreach (VectorSearchResult<ApiQueriesVectorStore> searchResult in vectorStore.SearchAsync(searchValue:input, top: numberOfSearchResults))
            {
                string result = $"Endpoint: {searchResult.Record.Method} /{searchResult.Record.Endpoint}. Description: {searchResult.Record.Description} Parameters: {searchResult.Record.Parameters}. RequestSchemas: {searchResult.Record.RequestSchemas}. ResponseSchemas: {searchResult.Record.ResponseSchemas} ,";
                mostSimilarknowledge.Append(result);
            }
            Console.WriteLine(mostSimilarknowledge.ToString());
            return mostSimilarknowledge.ToString();
        }
    }
}
