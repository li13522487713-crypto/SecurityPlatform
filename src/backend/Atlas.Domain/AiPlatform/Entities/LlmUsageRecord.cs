using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

[SugarTable("llm_usage_records")]
[SugarIndex(
    "IX_LlmUsageRecord_Tenant_CreatedAt",
    nameof(TenantIdValue),
    OrderByType.Asc,
    nameof(CreatedAt),
    OrderByType.Desc)]
public sealed class LlmUsageRecord : TenantEntity
{
    public LlmUsageRecord()
        : base(TenantId.Empty)
    {
        Provider = string.Empty;
        Model = string.Empty;
        Source = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public LlmUsageRecord(
        TenantId tenantId,
        long id,
        string provider,
        string? model,
        string? source,
        int promptTokens,
        int completionTokens,
        int totalTokens,
        decimal estimatedCostUsd)
        : base(tenantId)
    {
        Id = id;
        Provider = provider;
        Model = model ?? string.Empty;
        Source = source ?? string.Empty;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        TotalTokens = totalTokens;
        EstimatedCostUsd = estimatedCostUsd;
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(Length = 64)]
    public string Provider { get; private set; }

    [SugarColumn(Length = 128)]
    public string Model { get; private set; }

    [SugarColumn(Length = 128)]
    public string Source { get; private set; }

    public int PromptTokens { get; private set; }
    public int CompletionTokens { get; private set; }
    public int TotalTokens { get; private set; }
    public decimal EstimatedCostUsd { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
