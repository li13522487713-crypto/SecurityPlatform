using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class ProjectPositionRepository : IProjectPositionRepository
{
    private readonly ISqlSugarClient _db;

    public ProjectPositionRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProjectPosition>> QueryByProjectIdAsync(
        TenantId tenantId,
        long projectId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<ProjectPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<long>> QueryProjectIdsByPositionIdAsync(
        TenantId tenantId,
        long positionId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<ProjectPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.PositionId == positionId)
            .Select(x => x.ProjectId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task DeleteByProjectIdAsync(TenantId tenantId, long projectId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<ProjectPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProjectId == projectId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByPositionIdAsync(TenantId tenantId, long positionId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<ProjectPosition>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.PositionId == positionId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<ProjectPosition> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
