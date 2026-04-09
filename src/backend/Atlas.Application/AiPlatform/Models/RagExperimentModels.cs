namespace Atlas.Application.AiPlatform.Models;

public enum RagRetrieverStrategy
{
    Hybrid = 0,
    Vector = 1,
    Bm25 = 2
}

public sealed record RagExperimentDecision(
    string ExperimentName,
    string Variant,
    RagRetrieverStrategy PrimaryStrategy,
    bool ShadowEnabled,
    RagRetrieverStrategy? ShadowStrategy);

public sealed record RagExperimentRunCreateRequest(
    string ExperimentName,
    string Variant,
    RagRetrieverStrategy Strategy,
    string QueryHash,
    int TopK,
    IReadOnlyList<long> ChunkIds,
    int LatencyMs,
    bool IsShadow);

public sealed record RagExperimentRunDto(
    long Id,
    string ExperimentName,
    string Variant,
    string Strategy,
    string QueryHash,
    int TopK,
    int HitCount,
    int LatencyMs,
    bool IsShadow,
    DateTime CreatedAt);

public sealed record RagShadowComparisonDto(
    long Id,
    long MainRunId,
    long ShadowRunId,
    string ExperimentName,
    string MainVariant,
    string ShadowVariant,
    decimal OverlapScore,
    decimal MainAvgScore,
    decimal ShadowAvgScore,
    DateTime CreatedAt);
