using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// V2 工作流发布版本快照——发布时从 Draft 冻结一份 CanvasJson。
/// </summary>
[SugarTable("workflow_version")]
public sealed class WorkflowVersion : TenantEntity
{
    /// <summary>SqlSugar 无参构造。</summary>
    public WorkflowVersion() : base(TenantId.Empty)
    {
        CanvasJson = "{}";
    }

    public WorkflowVersion(
        TenantId tenantId,
        long workflowId,
        int versionNumber,
        string canvasJson,
        string? changeLog,
        long publishedByUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        WorkflowId = workflowId;
        VersionNumber = versionNumber;
        CanvasJson = string.IsNullOrWhiteSpace(canvasJson) ? "{}" : canvasJson;
        ChangeLog = changeLog;
        PublishedByUserId = publishedByUserId;
        PublishedAt = DateTime.UtcNow;
    }

    public long WorkflowId { get; private set; }
    public int VersionNumber { get; private set; }
    [SugarColumn(ColumnName = "canvas")]
    public string CanvasJson { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? ChangeLog { get; private set; }
    public DateTime PublishedAt { get; private set; }
    public long PublishedByUserId { get; private set; }
}
