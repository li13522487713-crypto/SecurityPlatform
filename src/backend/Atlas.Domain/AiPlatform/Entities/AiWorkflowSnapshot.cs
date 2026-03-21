using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// AI 工作流发布版本快照——每次 Publish 时固化当前 DefinitionJson / CanvasJson。
/// </summary>
public sealed class AiWorkflowSnapshot : TenantEntity
{
    /// <summary>SqlSugar 无参构造。</summary>
    public AiWorkflowSnapshot() : base(TenantId.Empty)
    {
        DefinitionJson = "{}";
        CanvasJson = "{}";
    }

    public AiWorkflowSnapshot(
        TenantId tenantId,
        long workflowDefinitionId,
        int version,
        string definitionJson,
        string canvasJson,
        string workflowName,
        long publishedByUserId,
        long id)
        : base(tenantId)
    {
        Id = id;
        WorkflowDefinitionId = workflowDefinitionId;
        Version = version;
        DefinitionJson = string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson;
        CanvasJson = string.IsNullOrWhiteSpace(canvasJson) ? "{}" : canvasJson;
        WorkflowName = workflowName;
        PublishedByUserId = publishedByUserId;
        PublishedAt = DateTime.UtcNow;
    }

    public long WorkflowDefinitionId { get; private set; }

    /// <summary>发布版本号，与 AiWorkflowDefinition.PublishVersion 对应。</summary>
    public int Version { get; private set; }

    public string DefinitionJson { get; private set; }
    public string CanvasJson { get; private set; }

    /// <summary>冗余存储发布时的工作流名称，便于历史版本展示。</summary>
    public string WorkflowName { get; private set; } = string.Empty;

    public long PublishedByUserId { get; private set; }
    public DateTime PublishedAt { get; private set; }

    [SugarColumn(IsNullable = true)]
    public string? ChangeLog { get; private set; }
}
