using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// Coze 原生工作流发布版本：冻结 Coze 原生 schema 快照。
/// </summary>
[SugarTable("coze_workflow_version")]
public sealed class CozeWorkflowVersion : TenantEntity
{
    public CozeWorkflowVersion() : base(TenantId.Empty)
    {
        SchemaJson = "{}";
    }

    public CozeWorkflowVersion(
        TenantId tenantId,
        long workflowId,
        int versionNumber,
        string schemaJson,
        string? changeLog,
        long publishedByUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        WorkflowId = workflowId;
        VersionNumber = versionNumber;
        SchemaJson = string.IsNullOrWhiteSpace(schemaJson) ? "{}" : schemaJson;
        ChangeLog = changeLog;
        PublishedByUserId = publishedByUserId;
        PublishedAt = DateTime.UtcNow;
    }

    public long WorkflowId { get; private set; }
    public int VersionNumber { get; private set; }
    [SugarColumn(ColumnName = "schema_json", ColumnDataType = "longtext")]
    public string SchemaJson { get; private set; }
    [SugarColumn(IsNullable = true)]
    public string? ChangeLog { get; private set; }
    public DateTime PublishedAt { get; private set; }
    public long PublishedByUserId { get; private set; }
}
