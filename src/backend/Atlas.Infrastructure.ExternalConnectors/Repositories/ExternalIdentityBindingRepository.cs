using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.ExternalConnectors.Repositories;

public sealed class ExternalIdentityBindingRepository : IExternalIdentityBindingRepository
{
    private readonly ISqlSugarClient _db;

    public ExternalIdentityBindingRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ExternalIdentityBinding entity, CancellationToken cancellationToken)
        => await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);

    public async Task UpdateAsync(ExternalIdentityBinding entity, CancellationToken cancellationToken)
        => await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);

    public async Task<ExternalIdentityBinding?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalIdentityBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);

    public async Task<ExternalIdentityBinding?> GetByExternalUserIdAsync(TenantId tenantId, long providerId, string externalUserId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalIdentityBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.ProviderId == providerId
                && x.ExternalUserId == externalUserId)
            .FirstAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalIdentityBinding>> GetByLocalUserIdAsync(TenantId tenantId, long localUserId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalIdentityBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.LocalUserId == localUserId)
            .OrderBy(x => x.ProviderId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalIdentityBinding>> ListConflictsAsync(TenantId tenantId, long providerId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalIdentityBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.ProviderId == providerId
                && x.Status == IdentityBindingStatus.Conflict)
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalIdentityBinding>> ListByProviderAsync(TenantId tenantId, long providerId, IdentityBindingStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        var query = _db.Queryable<ExternalIdentityBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId);
        if (status is { } s)
        {
            query = query.Where(x => x.Status == s);
        }
        return await query.OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByProviderAsync(TenantId tenantId, long providerId, IdentityBindingStatus? status, CancellationToken cancellationToken)
    {
        var query = _db.Queryable<ExternalIdentityBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId);
        if (status is { } s)
        {
            query = query.Where(x => x.Status == s);
        }
        return await query.CountAsync(cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
        => await _db.Deleteable<ExternalIdentityBinding>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
}
