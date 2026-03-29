using Atlas.Application.AiPlatform.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class WorkflowMetaRepository : RepositoryBase<WorkflowMeta>, IWorkflowMetaRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly IAppContextAccessor _appContextAccessor;

    public WorkflowMetaRepository(
        ISqlSugarClient db,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor) : base(db)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
        _appContextAccessor = appContextAccessor;
    }

    public WorkflowMetaRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<WorkflowMeta?> FindActiveByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var db = await ResolveDbAsync(tenantId, cancellationToken);
        return await db.Queryable<WorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id && !x.IsDeleted)
            .FirstAsync(cancellationToken);
    }

    public async Task<(List<WorkflowMeta> Items, long Total)> GetPagedAsync(
        TenantId tenantId, string? keyword, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        var db = await ResolveDbAsync(tenantId, cancellationToken);
        var query = db.Queryable<WorkflowMeta>()
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

    public async Task<(List<WorkflowMeta> Items, long Total)> GetPagedByStatusAsync(
        TenantId tenantId,
        WorkflowLifecycleStatus status,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var db = await ResolveDbAsync(tenantId, cancellationToken);
        var query = db.Queryable<WorkflowMeta>()
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

    public override async Task AddAsync(WorkflowMeta entity, CancellationToken cancellationToken)
    {
        var db = await ResolveDbAsync(new TenantId(entity.TenantIdValue), cancellationToken);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public override async Task UpdateAsync(WorkflowMeta entity, CancellationToken cancellationToken)
    {
        var db = await ResolveDbAsync(new TenantId(entity.TenantIdValue), cancellationToken);
        await db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<ISqlSugarClient> ResolveDbAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var appId = _appContextAccessor.ResolveAppId();
        if (appId.HasValue && appId.Value > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
        }

        return _mainDb;
    }
}
