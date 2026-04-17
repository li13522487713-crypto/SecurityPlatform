using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// 工作空间内的项目分组文件夹（PRD 03-5.4）。
/// 第一阶段不维护文件夹与对象的关联表，仅通过 <see cref="ItemCount"/> 累加计数。
/// </summary>
[SugarTable("WorkspaceFolder")]
public sealed class WorkspaceFolder : TenantEntity
{
    public WorkspaceFolder()
        : base(TenantId.Empty)
    {
        WorkspaceId = string.Empty;
        Name = string.Empty;
        Description = string.Empty;
        CreatedByDisplayName = string.Empty;
        CreatedAt = DateTime.UtcNow;
    }

    public WorkspaceFolder(
        TenantId tenantId,
        string workspaceId,
        string name,
        string description,
        long createdByUserId,
        string createdByDisplayName,
        long id)
        : base(tenantId)
    {
        Id = id;
        WorkspaceId = workspaceId;
        Name = name;
        Description = description;
        CreatedByUserId = createdByUserId;
        CreatedByDisplayName = createdByDisplayName;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string WorkspaceId { get; private set; }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string Name { get; private set; }

    [SugarColumn(Length = 1024, IsNullable = true)]
    public string Description { get; private set; }

    public int ItemCount { get; private set; }

    public long CreatedByUserId { get; private set; }

    [SugarColumn(Length = 128, IsNullable = false)]
    public string CreatedByDisplayName { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    public void Rename(string name, string description)
    {
        Name = name;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementItemCount(int delta = 1)
    {
        ItemCount += delta;
        if (ItemCount < 0)
        {
            ItemCount = 0;
        }
        UpdatedAt = DateTime.UtcNow;
    }
}
