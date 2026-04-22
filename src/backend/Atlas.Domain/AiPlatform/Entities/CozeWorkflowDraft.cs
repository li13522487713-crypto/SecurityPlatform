using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// Coze 原生工作流草稿：直接保存 Coze 原生 schema，不做 Atlas 画布归一化。
/// </summary>
public sealed class CozeWorkflowDraft : TenantEntity
{
    public CozeWorkflowDraft() : base(TenantId.Empty)
    {
        SchemaJson = "{}";
    }

    public CozeWorkflowDraft(
        TenantId tenantId,
        long workflowId,
        string schemaJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        WorkflowId = workflowId;
        SchemaJson = string.IsNullOrWhiteSpace(schemaJson) ? "{}" : schemaJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public long WorkflowId { get; private set; }
    public string SchemaJson { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? CommitId { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Save(string schemaJson, string? commitId)
    {
        SchemaJson = string.IsNullOrWhiteSpace(schemaJson) ? "{}" : schemaJson;
        CommitId = commitId;
        UpdatedAt = DateTime.UtcNow;
    }
}
