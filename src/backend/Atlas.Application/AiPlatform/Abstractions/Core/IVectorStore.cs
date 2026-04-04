using Atlas.Application.AiPlatform.Models;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IVectorStore
{
    Task EnsureCollectionAsync(string collectionName, int dimensions, CancellationToken ct = default);

    Task UpsertAsync(string collectionName, IEnumerable<VectorRecord> records, CancellationToken ct = default);

    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string collectionName,
        float[] queryVector,
        int topK = 5,
        CancellationToken ct = default);

    Task DeleteAsync(string collectionName, IEnumerable<string> ids, CancellationToken ct = default);
}
