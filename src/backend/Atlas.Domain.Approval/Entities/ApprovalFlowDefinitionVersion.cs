using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Approval.Entities;

/// <summary>
/// 审批流定义发布版本快照，用于历史追溯与回滚。
/// </summary>
public sealed class ApprovalFlowDefinitionVersion : TenantEntity
{
    public ApprovalFlowDefinitionVersion()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        DefinitionJson = string.Empty;
    }

    public ApprovalFlowDefinitionVersion(
        TenantId tenantId,
        long definitionId,
        int snapshotVersion,
        string name,
        string? description,
        string? category,
        string definitionJson,
        string? visibilityScopeJson,
        long createdBy,
        long id,
        DateTimeOffset createdAt)
        : base(tenantId)
    {
        Id = id;
        DefinitionId = definitionId;
        SnapshotVersion = snapshotVersion;
        Name = name;
        Description = description;
        Category = category;
        DefinitionJson = definitionJson;
        VisibilityScopeJson = visibilityScopeJson;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    /// <summary>审批流定义 ID</summary>
    public long DefinitionId { get; private set; }

    /// <summary>快照版本号（与 ApprovalFlowDefinition.Version 对应）</summary>
    public int SnapshotVersion { get; private set; }

    /// <summary>流程名称快照</summary>
    public string Name { get; private set; }

    /// <summary>描述快照</summary>
    public string? Description { get; private set; }

    /// <summary>分类快照</summary>
    public string? Category { get; private set; }

    /// <summary>定义 JSON 快照</summary>
    public string DefinitionJson { get; private set; }

    /// <summary>可见范围 JSON 快照</summary>
    public string? VisibilityScopeJson { get; private set; }

    /// <summary>创建人 ID（发布操作者）</summary>
    public long CreatedBy { get; private set; }

    /// <summary>发布时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }
}
