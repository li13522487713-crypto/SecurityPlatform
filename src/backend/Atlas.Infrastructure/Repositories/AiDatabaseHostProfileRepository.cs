using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class AiDatabaseHostProfileRepository : RepositoryBase<AiDatabaseHostProfile>
{
    public AiDatabaseHostProfileRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<List<AiDatabaseHostProfile>> ListAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiDatabaseHostProfile>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .OrderByDescending(x => x.IsDefault)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<AiDatabaseHostProfile?> FindDefaultAsync(TenantId tenantId, string driverCode, CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiDatabaseHostProfile>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DriverCode == driverCode && x.IsEnabled && x.IsDefault)
            .FirstAsync(cancellationToken);
    }

    public async Task<AiDatabaseHostProfile?> FindEnabledByDriverAsync(TenantId tenantId, string driverCode, CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiDatabaseHostProfile>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DriverCode == driverCode && x.IsEnabled)
            .OrderByDescending(x => x.IsDefault)
            .OrderBy(x => x.CreatedAt)
            .FirstAsync(cancellationToken);
    }

    public async Task ClearDefaultAsync(TenantId tenantId, string driverCode, long? exceptId, CancellationToken cancellationToken)
    {
        var items = await Db.Queryable<AiDatabaseHostProfile>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.DriverCode == driverCode && x.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (var item in items.Where(x => !exceptId.HasValue || x.Id != exceptId.Value))
        {
            item.MarkDefault(false);
            await UpdateAsync(item, cancellationToken);
        }
    }

    public async Task<bool> HasInstancesAsync(TenantId tenantId, long profileId, CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiDatabasePhysicalInstance>()
            .AnyAsync(x => x.TenantIdValue == tenantId.Value && x.HostProfileId == profileId, cancellationToken);
    }
}

public sealed class AiDatabasePhysicalInstanceRepository : RepositoryBase<AiDatabasePhysicalInstance>
{
    public AiDatabasePhysicalInstanceRepository(ISqlSugarClient db)
        : base(db)
    {
    }

    public async Task<List<AiDatabasePhysicalInstance>> ListByDatabaseAsync(
        TenantId tenantId,
        long databaseId,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiDatabasePhysicalInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AiDatabaseId == databaseId)
            .ToListAsync(cancellationToken);
    }

    public async Task<AiDatabasePhysicalInstance?> FindByDatabaseEnvironmentAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        return await Db.Queryable<AiDatabasePhysicalInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AiDatabaseId == databaseId && x.Environment == environment)
            .FirstAsync(cancellationToken);
    }

    public async Task DeleteByDatabaseAsync(TenantId tenantId, long databaseId, CancellationToken cancellationToken)
    {
        await Db.Deleteable<AiDatabasePhysicalInstance>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AiDatabaseId == databaseId)
            .ExecuteCommandAsync(cancellationToken);
    }
}
