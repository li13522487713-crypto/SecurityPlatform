using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.ExternalConnectors.Repositories;

public sealed class ExternalIdentityProviderRepository : IExternalIdentityProviderRepository
{
    private readonly ISqlSugarClient _db;

    public ExternalIdentityProviderRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ExternalIdentityProvider entity, CancellationToken cancellationToken)
        => await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);

    public async Task UpdateAsync(ExternalIdentityProvider entity, CancellationToken cancellationToken)
        => await _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);

    public async Task<ExternalIdentityProvider?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalIdentityProvider>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .FirstAsync(cancellationToken);

    public async Task<ExternalIdentityProvider?> GetByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalIdentityProvider>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Code == code)
            .FirstAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalIdentityProvider>> ListAsync(TenantId tenantId, ConnectorProviderType? type, bool includeDisabled, CancellationToken cancellationToken)
    {
        var query = _db.Queryable<ExternalIdentityProvider>()
            .Where(x => x.TenantIdValue == tenantId.Value);
        if (type is { } t)
        {
            query = query.Where(x => x.ProviderType == t);
        }
        if (!includeDisabled)
        {
            query = query.Where(x => x.Enabled);
        }
        return await query.OrderBy(x => x.ProviderType).OrderBy(x => x.Code).ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
        => await _db.Deleteable<ExternalIdentityProvider>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == id)
            .ExecuteCommandAsync(cancellationToken);
}
