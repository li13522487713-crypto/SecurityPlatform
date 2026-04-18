using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories.AiPlatform;

public sealed class WorkspaceChannelReleaseRepository : RepositoryBase<WorkspaceChannelRelease>
{
    public WorkspaceChannelReleaseRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<(IReadOnlyList<WorkspaceChannelRelease> Items, long Total)> SearchAsync(
        TenantId tenantId,
        string workspaceId,
        long channelId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var queryable = Db.Queryable<WorkspaceChannelRelease>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.WorkspaceId == workspaceId &&
                x.ChannelId == channelId);

        RefAsync<int> total = 0;
        var items = await queryable
            .OrderBy(x => x.ReleaseNo, OrderByType.Desc)
            .OrderBy(x => x.Id, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, total, cancellationToken);

        return (items, total.Value);
    }

    public async Task<WorkspaceChannelRelease?> FindAsync(
        TenantId tenantId,
        string workspaceId,
        long channelId,
        long releaseId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<WorkspaceChannelRelease>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.WorkspaceId == workspaceId &&
                x.ChannelId == channelId &&
                x.Id == releaseId)
            .FirstAsync(cancellationToken);
    }

    public async Task<WorkspaceChannelRelease?> FindActiveAsync(
        TenantId tenantId,
        string workspaceId,
        long channelId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<WorkspaceChannelRelease>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.WorkspaceId == workspaceId &&
                x.ChannelId == channelId &&
                x.Status == WorkspaceChannelRelease.StatusActive)
            .OrderBy(x => x.ReleaseNo, OrderByType.Desc)
            .FirstAsync(cancellationToken);
    }

    public async Task<int> GetMaxReleaseNoAsync(
        TenantId tenantId,
        string workspaceId,
        long channelId,
        CancellationToken cancellationToken)
    {
        var max = await Db.Queryable<WorkspaceChannelRelease>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.WorkspaceId == workspaceId &&
                x.ChannelId == channelId)
            .MaxAsync(x => (int?)x.ReleaseNo, cancellationToken);
        return max ?? 0;
    }
}
