using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicTableApprovalBindingRepository : IDynamicTableApprovalBindingRepository
{
    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly IAppContextAccessor _appContextAccessor;

    public DynamicTableApprovalBindingRepository(
        ISqlSugarClient mainDb,
        IAppDbScopeFactory appDbScopeFactory,
        IAppContextAccessor appContextAccessor)
    {
        _mainDb = mainDb;
        _appDbScopeFactory = appDbScopeFactory;
        _appContextAccessor = appContextAccessor;
    }

    public DynamicTableApprovalBindingRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db), NullAppContextAccessor.Instance)
    {
    }

    public async Task<DynamicTableApprovalBinding?> FindByTableKeyAsync(
        TenantId tenantId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, cancellationToken);
        return await db.Queryable<DynamicTableApprovalBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey == tableKey)
            .FirstAsync(cancellationToken);
    }

    public async Task UpsertAsync(
        DynamicTableApprovalBinding binding,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(new TenantId(binding.TenantIdValue), cancellationToken);
        var existing = await db.Queryable<DynamicTableApprovalBinding>()
            .Where(x => x.TenantIdValue == binding.TenantIdValue && x.TableKey == binding.TableKey)
            .FirstAsync(cancellationToken);

        if (existing is null)
        {
            await db.Insertable(binding).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await db.Updateable(binding)
                .Where(x => x.TenantIdValue == binding.TenantIdValue && x.TableKey == binding.TableKey)
                .ExecuteCommandAsync(cancellationToken);
        }
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
}
