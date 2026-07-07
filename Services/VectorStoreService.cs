using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData;
using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services
{
    public class VectorStoreService
    {
        public static async Task<bool> IngestData(VectorStoreCollection<Guid, ApiQueriesVectorStore> vectorStoreCollection, PlaygroundLoadResponse playgroundLoadResponse, CancellationToken cancellationToken = default)
        {
            await vectorStoreCollection.EnsureCollectionDeletedAsync(cancellationToken);
            await vectorStoreCollection.EnsureCollectionExistsAsync(cancellationToken);

            var allowedDomains = playgroundLoadResponse.AllowedDomains
                .Where(domain => !string.IsNullOrWhiteSpace(domain))
                .Select(domain => domain.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (allowedDomains.Length == 0)
            {
                return false;
            }

            foreach (var endpoint in playgroundLoadResponse.Endpoints)
            {
                if (string.IsNullOrWhiteSpace(endpoint.Path) || string.IsNullOrWhiteSpace(endpoint.Method))
                {
                    continue;
                }

                var normalizedPath = endpoint.Path.Trim();
                if (!normalizedPath.StartsWith('/'))
                {
                    normalizedPath = $"/{normalizedPath}";
                }

                var verb = endpoint.Method.Trim().ToUpperInvariant();
                var product = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "general";

                foreach (var domain in allowedDomains)
                {
                    var endpointWithDomain = $"{domain}{normalizedPath}";

                    await vectorStoreCollection.UpsertAsync(
                        new ApiQueriesVectorStore
                        {
                            Id = Guid.NewGuid(),
                            Verb = verb,
                            Endpoint = endpointWithDomain,
                            Product = product
                        },
                        cancellationToken);
                }
            }


            return true;
        }
    }
}
