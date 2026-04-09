using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

[SugarTable("rag_experiment_runs")]
[SugarIndex(
    "IX_RagExperimentRun_Tenant_CreatedAt",
    nameof(TenantIdValue),
    OrderByType.Asc,
    nameof(CreatedAt),
    OrderByType.Desc)]
public sealed class RagExperimentRun : TenantEntity
{
    public RagExperimentRun()
        : base(TenantId.Empty)
    {
        ExperimentName = string.Empty;
        Variant = string.Empty;
        Strategy = string.Empty;
        QueryHash = string.Empty;
        ChunkIdsJson = "[]";
        CreatedAt = DateTime.UtcNow;
    }

    public RagExperimentRun(
        TenantId tenantId,
        long id,
        string experimentName,
        string variant,
        string strategy,
        string queryHash,
        int topK,
        string chunkIdsJson,
        int hitCount,
        int latencyMs,
        bool isShadow,
        DateTime createdAt)
        : base(tenantId)
    {
        Id = id;
        ExperimentName = experimentName;
        Variant = variant;
        Strategy = strategy;
        QueryHash = queryHash;
        TopK = topK;
        ChunkIdsJson = chunkIdsJson;
        HitCount = hitCount;
        LatencyMs = latencyMs;
        IsShadow = isShadow;
        CreatedAt = createdAt;
    }

    [SugarColumn(Length = 64)]
    public string ExperimentName { get; private set; }

    [SugarColumn(Length = 64)]
    public string Variant { get; private set; }

    [SugarColumn(Length = 32)]
    public string Strategy { get; private set; }

    [SugarColumn(Length = 128)]
    public string QueryHash { get; private set; }

    public int TopK { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string ChunkIdsJson { get; private set; }

    public int HitCount { get; private set; }
    public int LatencyMs { get; private set; }
    public bool IsShadow { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

[SugarTable("rag_shadow_comparisons")]
[SugarIndex(
    "IX_RagShadowComparison_Tenant_CreatedAt",
    nameof(TenantIdValue),
    OrderByType.Asc,
    nameof(CreatedAt),
    OrderByType.Desc)]
public sealed class RagShadowComparison : TenantEntity
{
    public RagShadowComparison()
        : base(TenantId.Empty)
    {
        ExperimentName = string.Empty;
        MainVariant = string.Empty;
        ShadowVariant = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public RagShadowComparison(
        TenantId tenantId,
        long id,
        long mainRunId,
        long shadowRunId,
        string experimentName,
        string mainVariant,
        string shadowVariant,
        decimal overlapScore,
        decimal mainAvgScore,
        decimal shadowAvgScore,
        DateTime createdAt)
        : base(tenantId)
    {
        Id = id;
        MainRunId = mainRunId;
        ShadowRunId = shadowRunId;
        ExperimentName = experimentName;
        MainVariant = mainVariant;
        ShadowVariant = shadowVariant;
        OverlapScore = overlapScore;
        MainAvgScore = mainAvgScore;
        ShadowAvgScore = shadowAvgScore;
        CreatedAt = createdAt;
    }

    public long MainRunId { get; private set; }
    public long ShadowRunId { get; private set; }

    [SugarColumn(Length = 64)]
    public string ExperimentName { get; private set; }

    [SugarColumn(Length = 64)]
    public string MainVariant { get; private set; }

    [SugarColumn(Length = 64)]
    public string ShadowVariant { get; private set; }

    public decimal OverlapScore { get; private set; }
    public decimal MainAvgScore { get; private set; }
    public decimal ShadowAvgScore { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
