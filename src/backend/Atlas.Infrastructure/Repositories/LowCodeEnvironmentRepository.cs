using Atlas.Application.LowCode.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class LowCodeEnvironmentRepository : ILowCodeEnvironmentRepository
{
    private readonly ISqlSugarClient _db;

    public LowCodeEnvironmentRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<LowCodeEnvironment?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LowCodeEnvironment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);
    }

    public async Task<LowCodeEnvironment?> GetByCodeAsync(
        TenantId tenantId,
        long appId,
        string code,
        CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LowCodeEnvironment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Code == code)
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LowCodeEnvironment>> GetByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<LowCodeEnvironment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId)
            .OrderBy(x => new { isDefault = SqlFunc.Desc(x.IsDefault), updatedAt = SqlFunc.Desc(x.UpdatedAt) })
            .ToListAsync(cancellationToken);
    }

    public Task InsertAsync(LowCodeEnvironment entity, CancellationToken cancellationToken = default)
    {
        return _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public Task UpdateAsync(LowCodeEnvironment entity, CancellationToken cancellationToken = default)
    {
        return _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);
    }

    public Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        return _db.Deleteable<LowCodeEnvironment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(
        TenantId tenantId,
        long appId,
        string code,
        long? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<LowCodeEnvironment>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.Code == code);
        if (excludeId.HasValue)
        {
            query = query.Where(x => x.Id != excludeId.Value);
        }

        var count = await query.CountAsync(cancellationToken);
        return count > 0;
    }

    public Task ClearDefaultByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default)
    {
        return _db.Updateable<LowCodeEnvironment>()
            .SetColumns(x => x.IsDefault == false)
            .SetColumns(x => x.UpdatedAt == DateTimeOffset.UtcNow)
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == appId && x.IsDefault)
            .ExecuteCommandAsync(cancellationToken);
    }
}
