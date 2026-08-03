using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.VectorData;
using Softtek_APIExplorer_Backend.Models;

namespace Softtek_APIExplorer_Backend.Services
{
    public class VectorStoreService
    {
        public static async Task<bool> IngestData(VectorStoreCollection<Guid, ApiQueriesVectorStore> vectorStoreCollection, PlaygroundLoadResponse playgroundLoadResponse, CancellationToken cancellationToken = default)
        {
            try
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

                Dictionary<string, int> productCounts = new Dictionary<string, int>();

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
                        if (productCounts.ContainsKey(product))
                        {
                            productCounts[product]++;
                        }
                        else
                        {
                            productCounts.Add(product, 1);
                        }

                            await vectorStoreCollection.UpsertAsync(
                                new ApiQueriesVectorStore
                                {
                                    Id = Guid.NewGuid(),
                                    BaseUrl = domain,
                                    Endpoint = normalizedPath,
                                    Product = product,
                                    Method = verb,
                                    Summary = endpoint.Summary,
                                    Description = endpoint.Description,
                                    Parameters = string.Join(", ", endpoint.Parameters?.Select(p => p.ToString()).ToList() ?? new List<string>()),
                                    RequestSchemas = string.Join(", ", endpoint.RequestSchemas?.Select(s => s.ToString()).ToList() ?? new List<string>()),
                                    ResponseSchemas = string.Join(", ", endpoint.ResponseSchemas?.Select(s => s.ToString()).ToList() ?? new List<string>())
                                },
                                cancellationToken);
                    }
                }

                foreach (var resource in playgroundLoadResponse.Resources)
                {

                    foreach (var domain in allowedDomains)
                    {
                        
                        await vectorStoreCollection.UpsertAsync(
                            new ApiQueriesVectorStore
                            {
                                Id = Guid.NewGuid(),
                                BaseUrl = domain,
                                Product = resource?.Name,
                                Summary = resource?.Description,
                                Description = $"Overview of {resource.Name} and total count of {resource.Name} API endpoints available in knowledge base is: {productCounts[resource.Name]}",
                                Endpoint = string.Empty,
                                Method = string.Empty,
                                Parameters = string.Empty,
                                RequestSchemas = string.Empty,
                                ResponseSchemas = string.Empty,
                            },
                            cancellationToken);
                    }
                }
            }
            catch(Exception ex)
            {
                throw ex;
            }

            return true;
        }
    }
}
