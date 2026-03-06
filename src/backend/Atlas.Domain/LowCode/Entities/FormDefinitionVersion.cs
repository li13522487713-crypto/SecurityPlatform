using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 表单定义发布版本快照，用于历史追溯与回滚。
/// </summary>
public sealed class FormDefinitionVersion : TenantEntity
{
    public FormDefinitionVersion()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        SchemaJson = string.Empty;
    }

    public FormDefinitionVersion(
        TenantId tenantId,
        long formDefinitionId,
        int snapshotVersion,
        string name,
        string? description,
        string? category,
        string schemaJson,
        string? dataTableKey,
        string? icon,
        long createdBy,
        long id,
        DateTimeOffset createdAt)
        : base(tenantId)
    {
        Id = id;
        FormDefinitionId = formDefinitionId;
        SnapshotVersion = snapshotVersion;
        Name = name;
        Description = description;
        Category = category;
        SchemaJson = schemaJson;
        DataTableKey = dataTableKey;
        Icon = icon;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    /// <summary>表单定义 ID</summary>
    public long FormDefinitionId { get; private set; }

    /// <summary>快照版本号（与 FormDefinition.Version 对应）</summary>
    public int SnapshotVersion { get; private set; }

    /// <summary>表单名称快照</summary>
    public string Name { get; private set; }

    /// <summary>描述快照</summary>
    public string? Description { get; private set; }

    /// <summary>分类快照</summary>
    public string? Category { get; private set; }

    /// <summary>Schema JSON 快照</summary>
    public string SchemaJson { get; private set; }

    /// <summary>关联数据表 Key 快照</summary>
    public string? DataTableKey { get; private set; }

    /// <summary>图标快照</summary>
    public string? Icon { get; private set; }

    /// <summary>创建人 ID（发布操作者）</summary>
    public long CreatedBy { get; private set; }

    /// <summary>发布时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }
}
