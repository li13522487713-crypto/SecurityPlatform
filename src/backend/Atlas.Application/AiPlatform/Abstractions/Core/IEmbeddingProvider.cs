using Atlas.Application.AiPlatform.Models;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IEmbeddingProvider
{
    string ProviderName { get; }

    Task<EmbeddingResult> EmbedAsync(EmbeddingRequest request, CancellationToken ct = default);
}
