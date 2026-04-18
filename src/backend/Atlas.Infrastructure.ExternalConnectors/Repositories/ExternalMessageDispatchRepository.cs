using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;
using SqlSugar;

namespace Atlas.Infrastructure.ExternalConnectors.Repositories;

public sealed class ExternalMessageDispatchRepository : IExternalMessageDispatchRepository
{
    private readonly ISqlSugarClient _db;

    public ExternalMessageDispatchRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public Task AddAsync(ExternalMessageDispatch entity, CancellationToken cancellationToken)
        => _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);

    public Task UpdateAsync(ExternalMessageDispatch entity, CancellationToken cancellationToken)
        => _db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.TenantIdValue == entity.TenantIdValue)
            .ExecuteCommandAsync(cancellationToken);

    public async Task<ExternalMessageDispatch?> GetAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalMessageDispatch>()
            .Where(x => x.Id == id && x.TenantIdValue == tenantId.Value)
            .FirstAsync(cancellationToken);

    public async Task<ExternalMessageDispatch?> GetLatestByBusinessKeyAsync(TenantId tenantId, long providerId, string businessKey, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalMessageDispatch>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId && x.BusinessKey == businessKey)
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalMessageDispatch>> ListByProviderAsync(TenantId tenantId, long providerId, MessageDispatchStatus? status, int skip, int take, CancellationToken cancellationToken)
    {
        var query = _db.Queryable<ExternalMessageDispatch>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.ProviderId == providerId);
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }
        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }
}
