using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class ProjectUserRepository : IProjectUserRepository
{
    private readonly ISqlSugarClient _db;

    public ProjectUserRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProjectUser>> QueryByProjectIdAsync(
        TenantId tenantId,
        long projectId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<ProjectUser>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<long>> QueryUserIdsByProjectIdAsync(
        TenantId tenantId,
        long projectId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<ProjectUser>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProjectId == projectId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<long>> QueryProjectIdsByUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<ProjectUser>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .Select(x => x.ProjectId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<long>> QueryUserIdsByProjectIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> projectIds,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return Array.Empty<long>();
        }

        var ids = projectIds.Distinct().ToArray();
        var list = await _db.Queryable<ProjectUser>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(ids, x.ProjectId))
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);
        return list.Distinct().ToArray();
    }

    public async Task<IReadOnlyList<ProjectUser>> QueryByUserIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return Array.Empty<ProjectUser>();
        }

        var ids = userIds.Distinct().ToArray();
        var list = await _db.Queryable<ProjectUser>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(ids, x.UserId))
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<bool> ExistsAsync(TenantId tenantId, long projectId, long userId, CancellationToken cancellationToken)
    {
        return await _db.Queryable<ProjectUser>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProjectId == projectId && x.UserId == userId)
            .AnyAsync();
    }

    public Task DeleteByProjectIdAsync(TenantId tenantId, long projectId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<ProjectUser>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProjectId == projectId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<ProjectUser>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByUserAndProjectIdsAsync(
        TenantId tenantId,
        long userId,
        IReadOnlyList<long> projectIds,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var ids = projectIds.Distinct().ToArray();
        return _db.Deleteable<ProjectUser>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.UserId == userId && SqlFunc.ContainsArray(ids, x.ProjectId))
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<ProjectUser> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
