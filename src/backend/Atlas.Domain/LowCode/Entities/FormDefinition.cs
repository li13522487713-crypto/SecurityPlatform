using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Enums;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 表单定义（存储 amis JSON schema + 元数据，支持版本管理）
/// </summary>
public sealed class FormDefinition : TenantEntity
{
    public FormDefinition()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        SchemaJson = string.Empty;
    }

    public FormDefinition(
        TenantId tenantId,
        string name,
        string? description,
        string? category,
        string schemaJson,
        long createdBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = description;
        Category = category;
        SchemaJson = schemaJson;
        Version = 1;
        Status = FormDefinitionStatus.Draft;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
        PublishedAt = null;
        PublishedBy = null;
    }

    /// <summary>表单名称</summary>
    public string Name { get; private set; }

    /// <summary>表单描述</summary>
    public string? Description { get; private set; }

    /// <summary>分类（如：人事类、财务类、采购类）</summary>
    public string? Category { get; private set; }

    /// <summary>amis JSON Schema</summary>
    public string SchemaJson { get; private set; }

    /// <summary>版本号</summary>
    public int Version { get; private set; }

    /// <summary>状态</summary>
    public FormDefinitionStatus Status { get; private set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>创建人 ID</summary>
    public long CreatedBy { get; private set; }

    /// <summary>更新人 ID</summary>
    public long UpdatedBy { get; private set; }

    /// <summary>发布时间</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>发布人 ID</summary>
    public long? PublishedBy { get; private set; }

    /// <summary>关联的数据表 Key（用于表单数据持久化）</summary>
    public string? DataTableKey { get; private set; }

    /// <summary>图标</summary>
    public string? Icon { get; private set; }

    public void Update(
        string name,
        string? description,
        string? category,
        string schemaJson,
        long updatedBy,
        DateTimeOffset now)
    {
        Name = name;
        Description = description;
        Category = category;
        SchemaJson = schemaJson;
        Version += 1;
        UpdatedBy = updatedBy;
        UpdatedAt = now;

        if (Status != FormDefinitionStatus.Draft)
        {
            Status = FormDefinitionStatus.Draft;
        }
    }

    public void UpdateSchema(string schemaJson, long updatedBy, DateTimeOffset now)
    {
        SchemaJson = schemaJson;
        Version += 1;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Publish(long publishedBy, DateTimeOffset now)
    {
        Status = FormDefinitionStatus.Published;
        PublishedAt = now;
        PublishedBy = publishedBy;
        UpdatedBy = publishedBy;
        UpdatedAt = now;
    }

    public void Disable(long updatedBy, DateTimeOffset now)
    {
        Status = FormDefinitionStatus.Disabled;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Enable(long updatedBy, DateTimeOffset now)
    {
        Status = FormDefinitionStatus.Published;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Archive(long updatedBy, DateTimeOffset now)
    {
        Status = FormDefinitionStatus.Archived;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void BindDataTable(string dataTableKey, long updatedBy, DateTimeOffset now)
    {
        DataTableKey = dataTableKey;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void SetIcon(string? icon, long updatedBy, DateTimeOffset now)
    {
        Icon = icon;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
