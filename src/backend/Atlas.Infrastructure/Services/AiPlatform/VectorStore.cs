using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class VectorStore : IVectorStore
{
    private readonly IReadOnlyDictionary<string, IVectorDbClient> _clients;
    private readonly IOptionsMonitor<AiPlatformOptions> _optionsMonitor;
    private readonly ILogger<VectorStore> _logger;

    public VectorStore(
        IEnumerable<IVectorDbClient> clients,
        IOptionsMonitor<AiPlatformOptions> options,
        ILogger<VectorStore> logger)
    {
        _clients = clients.ToDictionary(client => client.ProviderName, client => client, StringComparer.OrdinalIgnoreCase);
        _optionsMonitor = options;
        _logger = logger;
    }

    public Task EnsureCollectionAsync(string collectionName, int dimensions, CancellationToken ct = default)
    {
        return ResolveClient().EnsureCollectionAsync(collectionName, dimensions, ct);
    }

    public Task UpsertAsync(string collectionName, IEnumerable<VectorRecord> records, CancellationToken ct = default)
    {
        return ResolveClient().UpsertAsync(collectionName, records, ct);
    }

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collectionName,
        float[] queryVector,
        int topK = 5,
        CancellationToken ct = default)
    {
        return ResolveClient().SearchAsync(collectionName, queryVector, topK, ct);
    }

    public Task DeleteAsync(string collectionName, IEnumerable<string> ids, CancellationToken ct = default)
    {
        return ResolveClient().DeleteAsync(collectionName, ids, ct);
    }

    private IVectorDbClient ResolveClient()
    {
        var provider = _optionsMonitor.CurrentValue.VectorDb.Provider;
        if (_clients.TryGetValue(provider, out var client))
        {
            return client;
        }

        if (_clients.TryGetValue("sqlite", out var sqlite))
        {
            _logger.LogWarning("Vector provider {Provider} not found, fallback to sqlite.", provider);
            return sqlite;
        }

        throw new InvalidOperationException("No vector database client is registered.");
    }
}
