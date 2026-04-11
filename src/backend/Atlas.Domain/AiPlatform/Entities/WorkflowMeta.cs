using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Enums;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// V2 工作流元数据（草稿-发布模型的核心实体）。
/// </summary>
public sealed class WorkflowMeta : TenantEntity
{
    /// <summary>SqlSugar 无参构造。</summary>
    public WorkflowMeta() : base(TenantId.Empty)
    {
        Name = string.Empty;
    }

    public WorkflowMeta(
        TenantId tenantId,
        string name,
        string? description,
        WorkflowMode mode,
        long creatorId,
        long id)
        : base(tenantId)
    {
        Id = id;
        Name = name;
        Description = NormalizeDescription(description);
        Mode = mode;
        Status = WorkflowLifecycleStatus.Draft;
        LatestVersionNumber = 0;
        CreatorId = creatorId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsDeleted = false;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }
    public WorkflowMode Mode { get; private set; }
    public WorkflowLifecycleStatus Status { get; private set; }
    public int LatestVersionNumber { get; private set; }
    public long CreatorId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? PublishedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    public void UpdateMeta(string name, string? description)
    {
        Name = name;
        Description = NormalizeDescription(description);
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
    }

    public void MarkPublished(int versionNumber)
    {
        Status = WorkflowLifecycleStatus.Published;
        LatestVersionNumber = versionNumber;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkArchived()
    {
        Status = WorkflowLifecycleStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
