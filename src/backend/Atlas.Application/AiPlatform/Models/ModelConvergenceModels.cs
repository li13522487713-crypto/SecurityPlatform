namespace Atlas.Application.AiPlatform.Models;

public sealed record ModelConvergenceDiffItem(
    string Provider,
    string ModelName,
    bool SupportsEmbedding,
    bool EnableStreaming,
    bool EnableTools,
    bool EnableJsonMode,
    string BaseUrl);

public sealed record ModelConvergenceProfile(
    string RecommendedProvider,
    string RecommendedModel,
    string EmbeddingProvider,
    string EmbeddingModel,
    IReadOnlyList<string> Reasons);

public sealed record ModelConvergenceResponse(
    IReadOnlyList<ModelConvergenceDiffItem> Diffs,
    ModelConvergenceProfile Profile);
