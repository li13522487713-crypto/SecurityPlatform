using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// V2 工作流草稿——每个工作流有且仅有一条草稿记录，保存最新编辑状态。
/// </summary>
[SugarTable("workflow_draft")]
public sealed class WorkflowDraft : TenantEntity
{
    /// <summary>SqlSugar 无参构造。</summary>
    public WorkflowDraft() : base(TenantId.Empty)
    {
        CanvasJson = "{}";
    }

    public WorkflowDraft(
        TenantId tenantId,
        long workflowId,
        string canvasJson,
        long id)
        : base(tenantId)
    {
        Id = id;
        WorkflowId = workflowId;
        CanvasJson = string.IsNullOrWhiteSpace(canvasJson) ? "{}" : canvasJson;
        UpdatedAt = DateTime.UtcNow;
    }

    public long WorkflowId { get; private set; }
    [SugarColumn(ColumnName = "canvas")]
    public string CanvasJson { get; private set; }
    [SugarColumn(ColumnName = "commit_id", IsNullable = true)]
    public string? CommitId { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Save(string canvasJson, string? commitId)
    {
        CanvasJson = string.IsNullOrWhiteSpace(canvasJson) ? "{}" : canvasJson;
        CommitId = commitId;
        UpdatedAt = DateTime.UtcNow;
    }
}
