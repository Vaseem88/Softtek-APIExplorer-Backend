using Microsoft.Extensions.VectorData;
using System.Text;

namespace Softtek_APIExplorer_Backend.Models
{
    public class ApiQueriesVectorStore
    {
        [VectorStoreKey]
        public Guid Id { get; set; }

        [VectorStoreData]
        public string Verb { get; set; }

        [VectorStoreData]
        public string Endpoint { get; set; }

        [VectorStoreData]
        public string Product { get; set; }

        [VectorStoreVector(1536)]
        public string Vector => $"{Verb}:{Product}:{Endpoint}";

    }

    public class SearchTool(VectorStoreCollection<Guid, ApiQueriesVectorStore> vectorStore)
    {
        public async Task<string> Search(string input)
        {
            StringBuilder mostSimilarknowledge = new StringBuilder();
            int numberOfSearchResults = 3;

            await foreach (VectorSearchResult<ApiQueriesVectorStore> searchResult in vectorStore.SearchAsync(searchValue:input, top: numberOfSearchResults))
            {
                string result = $"{searchResult.Record.Verb}:{searchResult.Record.Id}:{searchResult.Record.Product}:{searchResult.Record.Endpoint} ,";
                mostSimilarknowledge.Append(result);
            }
            return mostSimilarknowledge.ToString();
        }
    }
}
