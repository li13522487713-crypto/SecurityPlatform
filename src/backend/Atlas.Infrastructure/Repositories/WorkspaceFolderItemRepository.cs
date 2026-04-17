using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class WorkspaceFolderItemRepository : RepositoryBase<WorkspaceFolderItem>
{
    public WorkspaceFolderItemRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    /// <summary>
    /// 查询指定 (type, id) 在当前工作空间已存在的关联。
    /// </summary>
    public async Task<WorkspaceFolderItem?> FindAssignmentAsync(
        TenantId tenantId,
        string workspaceId,
        string itemType,
        string itemId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<WorkspaceFolderItem>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.WorkspaceId == workspaceId
                && x.ItemType == itemType
                && x.ItemId == itemId)
            .FirstAsync(cancellationToken);
    }

    /// <summary>
    /// 按 folderId 批量统计当前包含的对象数。
    /// 一次性聚合，禁止在循环里查库。
    /// </summary>
    public async Task<IReadOnlyDictionary<long, int>> CountByFolderIdsAsync(
        TenantId tenantId,
        string workspaceId,
        IReadOnlyList<long> folderIds,
        CancellationToken cancellationToken)
    {
        if (folderIds.Count == 0)
        {
            return new Dictionary<long, int>();
        }

        var idArray = folderIds.Distinct().ToArray();
        var rows = await Db.Queryable<WorkspaceFolderItem>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.WorkspaceId == workspaceId
                && SqlFunc.ContainsArray(idArray, x.FolderId))
            .GroupBy(x => x.FolderId)
            .Select(x => new { x.FolderId, Count = SqlFunc.AggregateCount(x.Id) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.FolderId, r => r.Count);
    }

    /// <summary>
    /// 按 folderId 删除所有关联（删除文件夹时调用）。
    /// </summary>
    public Task DeleteByFolderAsync(
        TenantId tenantId,
        string workspaceId,
        long folderId,
        CancellationToken cancellationToken)
    {
        return Db.Deleteable<WorkspaceFolderItem>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.WorkspaceId == workspaceId
                && x.FolderId == folderId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
