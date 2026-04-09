using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

[SugarTable("agentic_rag_run_histories")]
[SugarIndex(
    "IX_AgenticRagRunHistory_Tenant_CreatedAt",
    nameof(TenantIdValue),
    OrderByType.Asc,
    nameof(CreatedAt),
    OrderByType.Desc)]
public sealed class AgenticRagRunHistory : TenantEntity
{
    public AgenticRagRunHistory()
        : base(TenantId.Empty)
    {
        SessionKey = string.Empty;
        Query = string.Empty;
        FinalAnswer = string.Empty;
        CitationsJson = "[]";
        TracesJson = "[]";
    }

    public AgenticRagRunHistory(
        TenantId tenantId,
        long id,
        string sessionKey,
        string query,
        string finalAnswer,
        string citationsJson,
        string tracesJson)
        : base(tenantId)
    {
        Id = id;
        SessionKey = sessionKey;
        Query = query;
        FinalAnswer = finalAnswer;
        CitationsJson = citationsJson;
        TracesJson = tracesJson;
        CreatedAt = DateTime.UtcNow;
    }

    [SugarColumn(Length = 64)]
    public string SessionKey { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string Query { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string FinalAnswer { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string CitationsJson { get; private set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string TracesJson { get; private set; }

    public DateTime CreatedAt { get; private set; }
}
