using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Enums;

namespace Atlas.Domain.LowCode.Entities;

/// <summary>
/// 低代码应用（一个应用包含多个页面、表单、工作流的聚合）
/// </summary>
public sealed class LowCodeApp : TenantEntity
{
    public LowCodeApp()
        : base(TenantId.Empty)
    {
        Name = string.Empty;
        AppKey = string.Empty;
    }

    public LowCodeApp(
        TenantId tenantId,
        string appKey,
        string name,
        string? description,
        string? category,
        string? icon,
        long createdBy,
        long id,
        DateTimeOffset now)
        : base(tenantId)
    {
        Id = id;
        AppKey = appKey;
        Name = name;
        Description = description;
        Category = category;
        Icon = icon;
        Version = 1;
        Status = LowCodeAppStatus.Draft;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
    }

    /// <summary>应用唯一标识</summary>
    public string AppKey { get; private set; }

    /// <summary>应用名称</summary>
    public string Name { get; private set; }

    /// <summary>应用描述</summary>
    public string? Description { get; private set; }

    /// <summary>分类</summary>
    public string? Category { get; private set; }

    /// <summary>图标</summary>
    public string? Icon { get; private set; }

    /// <summary>版本号</summary>
    public int Version { get; private set; }

    /// <summary>状态</summary>
    public LowCodeAppStatus Status { get; private set; }

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

    /// <summary>应用配置 JSON（菜单树、权限映射等）</summary>
    public string? ConfigJson { get; private set; }

    public void Update(
        string name,
        string? description,
        string? category,
        string? icon,
        long updatedBy,
        DateTimeOffset now)
    {
        Name = name;
        Description = description;
        Category = category;
        Icon = icon;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void UpdateConfig(string configJson, long updatedBy, DateTimeOffset now)
    {
        ConfigJson = configJson;
        Version += 1;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Publish(long publishedBy, DateTimeOffset now)
    {
        Status = LowCodeAppStatus.Published;
        PublishedAt = now;
        PublishedBy = publishedBy;
        UpdatedBy = publishedBy;
        UpdatedAt = now;
    }

    public void Disable(long updatedBy, DateTimeOffset now)
    {
        Status = LowCodeAppStatus.Disabled;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Enable(long updatedBy, DateTimeOffset now)
    {
        Status = LowCodeAppStatus.Published;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }

    public void Archive(long updatedBy, DateTimeOffset now)
    {
        Status = LowCodeAppStatus.Archived;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
