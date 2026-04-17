using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class WorkspaceFolderRepository : RepositoryBase<WorkspaceFolder>
{
    public WorkspaceFolderRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(IReadOnlyList<WorkspaceFolder> Items, long Total)> SearchAsync(
        TenantId tenantId,
        string workspaceId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var queryable = Db.Queryable<WorkspaceFolder>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            queryable = queryable.Where(x => x.Name.Contains(keyword));
        }

        RefAsync<int> total = 0;
        var items = await queryable
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        return (items, total.Value);
    }

    public async Task<WorkspaceFolder?> FindAsync(
        TenantId tenantId,
        string workspaceId,
        long id,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<WorkspaceFolder>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId && x.Id == id)
            .FirstAsync(cancellationToken);
    }
}

public sealed class WorkspacePublishChannelRepository : RepositoryBase<WorkspacePublishChannel>
{
    public WorkspacePublishChannelRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(IReadOnlyList<WorkspacePublishChannel> Items, long Total)> SearchAsync(
        TenantId tenantId,
        string workspaceId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var queryable = Db.Queryable<WorkspacePublishChannel>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            queryable = queryable.Where(x => x.Name.Contains(keyword));
        }

        RefAsync<int> total = 0;
        var items = await queryable
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        return (items, total.Value);
    }

    public async Task<WorkspacePublishChannel?> FindAsync(
        TenantId tenantId,
        string workspaceId,
        long id,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<WorkspacePublishChannel>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId && x.Id == id)
            .FirstAsync(cancellationToken);
    }
}
