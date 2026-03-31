using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly ISqlSugarClient _db;

    public ProjectRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<Project?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _db.Queryable<Project>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<Project?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
    {
        return await _db.Queryable<Project>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Code == code)
            .FirstAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Project> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<Project>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var list = await query
            .OrderBy(x => x.SortOrder, OrderByType.Asc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (list, totalCount);
    }

    public async Task<IReadOnlyList<Project>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<Project>();
        }

        var idArray = ids.Distinct().ToArray();
        return await _db.Queryable<Project>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(idArray, x.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Project> Items, int TotalCount)> QueryPagedByUserIdAsync(
        TenantId tenantId,
        long userId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken)
    {
        var query = _db.Queryable<ProjectUser, Project>(
                (pu, p) => new JoinQueryInfos(JoinType.Inner, pu.ProjectId == p.Id))
            .Where((pu, p) => pu.TenantIdValue == tenantId.Value
                && p.TenantIdValue == tenantId.Value
                && pu.UserId == userId);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where((pu, p) => p.Code.Contains(keyword) || p.Name.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy((pu, p) => p.SortOrder, OrderByType.Asc)
            .OrderBy((pu, p) => p.Id, OrderByType.Asc)
            .Select((pu, p) => p)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        return (items, totalCount);
    }

    public Task AddAsync(Project project, CancellationToken cancellationToken)
    {
        return _db.Insertable(project).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(Project project, CancellationToken cancellationToken)
    {
        return _db.Updateable(project)
            .Where(x => x.Id == project.Id && x.TenantIdValue == project.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return _db.Deleteable<Project>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }
}
