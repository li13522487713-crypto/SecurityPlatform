using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using SqlSugar;

namespace Atlas.Domain.AiPlatform.Entities;

/// <summary>
/// 文件夹与对象（智能体 / 应用 / 项目）的关联（PRD 03-5.4 移入文件夹）。
/// 一个对象在一个工作空间内只允许加入一个文件夹（同 type+id 唯一）。
/// </summary>
[SugarTable("WorkspaceFolderItem")]
public sealed class WorkspaceFolderItem : TenantEntity
{
    public WorkspaceFolderItem()
        : base(TenantId.Empty)
    {
        WorkspaceId = string.Empty;
        ItemType = string.Empty;
        ItemId = string.Empty;
        AddedAt = DateTime.UtcNow;
    }

    public WorkspaceFolderItem(
        TenantId tenantId,
        string workspaceId,
        long folderId,
        string itemType,
        string itemId,
        long id)
        : base(tenantId)
    {
        Id = id;
        WorkspaceId = workspaceId;
        FolderId = folderId;
        ItemType = itemType;
        ItemId = itemId;
        AddedAt = DateTime.UtcNow;
    }

    [SugarColumn(Length = 64, IsNullable = false)]
    public string WorkspaceId { get; private set; }

    public long FolderId { get; private set; }

    /// <summary>
    /// agent / app / project 三选一。
    /// </summary>
    [SugarColumn(Length = 16, IsNullable = false)]
    public string ItemType { get; private set; }

    /// <summary>
    /// 对象 ID（智能体或应用的字符串 ID）。
    /// </summary>
    [SugarColumn(Length = 64, IsNullable = false)]
    public string ItemId { get; private set; }

    public DateTime AddedAt { get; private set; }
}
