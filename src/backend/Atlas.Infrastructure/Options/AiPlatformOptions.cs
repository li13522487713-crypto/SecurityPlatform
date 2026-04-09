namespace Atlas.Infrastructure.Options;

public sealed class AiPlatformOptions
{
    public string DefaultProvider { get; init; } = "openai";

    public Dictionary<string, AiProviderOption> Providers { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public EmbeddingOption Embedding { get; init; } = new();

    public VectorDbOption VectorDb { get; init; } = new();

    public RetrievalOption Retrieval { get; init; } = new();

    public RagExperimentOption RagExperiment { get; init; } = new();

    public MemoryOption Memory { get; init; } = new();

    public AgentPublicationOption Publication { get; init; } = new();

    public OpenApiProjectOption OpenApiProject { get; init; } = new();

    public OpenApiGovernanceOption OpenApiGovernance { get; init; } = new();
}

public sealed class AiProviderOption
{
    public string ApiKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public string DefaultModel { get; init; } = string.Empty;

    public bool SupportsEmbedding { get; init; } = true;
}

public sealed class EmbeddingOption
{
    public string Provider { get; init; } = "openai";

    public string Model { get; init; } = "text-embedding-3-small";

    public int Dimensions { get; init; } = 1536;
}

public sealed class VectorDbOption
{
    public string Provider { get; init; } = "sqlite";

    public string SqliteConnectionString { get; init; } = string.Empty;

    public string QdrantUrl { get; init; } = "http://localhost:6333";

    public string QdrantApiKey { get; init; } = string.Empty;
}

public sealed class RetrievalOption
{
    public bool EnableHybrid { get; init; } = true;

    public bool EnableRerank { get; init; } = true;

    public bool EnableCrossEncoderRerank { get; init; } = true;

    public bool EnableContextCompression { get; init; } = true;

    public bool EnableFreshnessBoost { get; init; } = true;

    public int VectorTopK { get; init; } = 12;

    public int Bm25TopK { get; init; } = 12;

    public int Bm25CandidateCount { get; init; } = 300;

    public int RrfK { get; init; } = 60;

    public int CrossEncoderTopK { get; init; } = 16;

    public int ContextMaxChars { get; init; } = 900;

    public int FreshnessHalfLifeDays { get; init; } = 30;
}

public sealed class RagExperimentOption
{
    public bool Enabled { get; init; } = true;

    public string ExperimentName { get; init; } = "rag_retriever_ab_v1";

    public int TrafficPercent { get; init; } = 100;

    public int ControlPercent { get; init; } = 50;

    public string ControlStrategy { get; init; } = "hybrid";

    public string TreatmentStrategy { get; init; } = "vector";

    public bool ShadowEnabled { get; init; } = true;

    public int ShadowTrafficPercent { get; init; } = 30;

    public string ShadowStrategy { get; init; } = "bm25";
}

public sealed class MemoryOption
{
    public bool Enabled { get; init; } = true;

    public int ShortTermTriggerMessageCount { get; init; } = 10;

    public int ShortTermReserveRecentMessages { get; init; } = 4;

    public int ShortTermMinIncrementalMessages { get; init; } = 3;

    public int ShortTermMaxSummaryLength { get; init; } = 1200;

    public int LongTermRecallTopK { get; init; } = 3;

    public int LongTermCandidateCount { get; init; } = 30;

    public int LongTermMaxRecordsPerUserAgent { get; init; } = 200;
}

public sealed class AgentPublicationOption
{
    public int EmbedTokenTtlHours { get; init; } = 24 * 30;
}

public sealed class OpenApiProjectOption
{
    public int AccessTokenExpiresMinutes { get; init; } = 60;
}

public sealed class OpenApiGovernanceOption
{
    public int ProjectRateLimitPerMinute { get; init; } = 120;
}
