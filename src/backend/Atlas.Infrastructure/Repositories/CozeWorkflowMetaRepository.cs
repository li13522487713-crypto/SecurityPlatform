using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class CozeWorkflowMetaRepository : RepositoryBase<CozeWorkflowMeta>, ICozeWorkflowMetaRepository
{
    private readonly ISqlSugarClient _mainDb;

    public CozeWorkflowMetaRepository(
        ISqlSugarClient db,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor) : base(db)
    {
        _mainDb = db;
    }

    public CozeWorkflowMetaRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<CozeWorkflowMeta?> FindActiveByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<CozeWorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id && !x.IsDeleted)
            .FirstAsync(cancellationToken);
    }

    public async Task<(List<CozeWorkflowMeta> Items, long Total)> GetPagedAsync(
        TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var query = _mainDb.Queryable<CozeWorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x => x.Name.Contains(normalized) || (x.Description != null && x.Description.Contains(normalized)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public async Task<(List<CozeWorkflowMeta> Items, long Total)> GetPagedByStatusAsync(
        TenantId tenantId,
        WorkflowLifecycleStatus status,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _mainDb.Queryable<CozeWorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsDeleted && x.Status == status);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x => x.Name.Contains(normalized) || (x.Description != null && x.Description.Contains(normalized)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return (items, total);
    }

    public override async Task AddAsync(CozeWorkflowMeta entity, CancellationToken cancellationToken)
    {
        await _mainDb.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public override async Task UpdateAsync(CozeWorkflowMeta entity, CancellationToken cancellationToken)
    {
        await _mainDb.Updateable<CozeWorkflowMeta>()
            .SetColumns(x => x.Name == entity.Name)
            .SetColumns(x => x.Description == entity.Description)
            .SetColumns(x => x.Status == entity.Status)
            .SetColumns(x => x.LatestVersionNumber == entity.LatestVersionNumber)
            .SetColumns(x => x.UpdatedAt == entity.UpdatedAt)
            .SetColumns(x => x.PublishedAt == entity.PublishedAt)
            .SetColumns(x => x.IsDeleted == entity.IsDeleted)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }
}
