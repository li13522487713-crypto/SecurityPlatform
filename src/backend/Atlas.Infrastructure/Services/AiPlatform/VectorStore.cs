using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class VectorStore : IVectorStore
{
    private readonly IVectorDbClient _client;

    public VectorStore(
        IEnumerable<IVectorDbClient> clients,
        IOptions<AiPlatformOptions> options,
        ILogger<VectorStore> logger)
    {
        var provider = options.Value.VectorDb.Provider;
        _client = clients.FirstOrDefault(client =>
                      string.Equals(client.ProviderName, provider, StringComparison.OrdinalIgnoreCase))
                  ?? clients.FirstOrDefault(client =>
                      string.Equals(client.ProviderName, "sqlite", StringComparison.OrdinalIgnoreCase))
                  ?? throw new InvalidOperationException("No vector database client is registered.");

        logger.LogInformation(
            "VectorStore initialized with provider {Provider}.",
            _client.ProviderName);
    }

    public Task EnsureCollectionAsync(string collectionName, int dimensions, CancellationToken ct = default)
    {
        return _client.EnsureCollectionAsync(collectionName, dimensions, ct);
    }

    public Task UpsertAsync(string collectionName, IEnumerable<VectorRecord> records, CancellationToken ct = default)
    {
        return _client.UpsertAsync(collectionName, records, ct);
    }

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collectionName,
        float[] queryVector,
        int topK = 5,
        CancellationToken ct = default)
    {
        return _client.SearchAsync(collectionName, queryVector, topK, ct);
    }

    public Task DeleteAsync(string collectionName, IEnumerable<string> ids, CancellationToken ct = default)
    {
        return _client.DeleteAsync(collectionName, ids, ct);
    }
}
