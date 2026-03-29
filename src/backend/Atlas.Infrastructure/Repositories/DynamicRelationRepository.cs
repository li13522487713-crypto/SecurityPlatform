using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicRelationRepository : IDynamicRelationRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly IAppContextAccessor _appContextAccessor;

    public DynamicRelationRepository(
        ISqlSugarClient mainDb,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor)
    {
        _mainDb = mainDb;
        _appDbScopeFactory = appDbScopeFactory;
        _appContextAccessor = appContextAccessor;
    }

    public DynamicRelationRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<IReadOnlyList<DynamicRelation>> ListByTableIdAsync(
        TenantId tenantId,
        long tableId,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, cancellationToken);
        var list = await db.Queryable<DynamicRelation>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == tableId)
            .ToListAsync(cancellationToken);
        return list;
    }

    public Task AddRangeAsync(
        IReadOnlyList<DynamicRelation> relations,
        CancellationToken cancellationToken)
    {
        if (relations.Count == 0)
        {
            return Task.CompletedTask;
        }

        return ExecuteInDbAsync(
            new TenantId(relations[0].TenantIdValue),
            db => db.Insertable(relations.ToList()).ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    public Task DeleteByTableIdAsync(
        TenantId tenantId,
        long tableId,
        CancellationToken cancellationToken)
    {
        return ExecuteInDbAsync(
            tenantId,
            db => db.Deleteable<DynamicRelation>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == tableId)
                .ExecuteCommandAsync(cancellationToken),
            cancellationToken);
    }

    private async Task<ISqlSugarClient> GetDbAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var appId = _appContextAccessor.ResolveAppId();
        if (appId.HasValue && appId.Value > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
        }

        return _mainDb;
    }

    private async Task ExecuteInDbAsync(
        TenantId tenantId,
        Func<ISqlSugarClient, Task> operation,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, cancellationToken);
        await operation(db);
    }
}
