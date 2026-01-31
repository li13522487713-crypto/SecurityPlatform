using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class ProjectDepartmentRepository : IProjectDepartmentRepository
{
    private readonly ISqlSugarClient _db;

    public ProjectDepartmentRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProjectDepartment>> QueryByProjectIdAsync(
        TenantId tenantId,
        long projectId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<ProjectDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProjectId == projectId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public async Task<IReadOnlyList<long>> QueryProjectIdsByDepartmentIdAsync(
        TenantId tenantId,
        long departmentId,
        CancellationToken cancellationToken)
    {
        var list = await _db.Queryable<ProjectDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DepartmentId == departmentId)
            .Select(x => x.ProjectId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task DeleteByProjectIdAsync(TenantId tenantId, long projectId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<ProjectDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProjectId == projectId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteByDepartmentIdAsync(TenantId tenantId, long departmentId, CancellationToken cancellationToken)
    {
        return _db.Deleteable<ProjectDepartment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DepartmentId == departmentId)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task AddRangeAsync(IReadOnlyList<ProjectDepartment> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _db.Insertable(entities.ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
