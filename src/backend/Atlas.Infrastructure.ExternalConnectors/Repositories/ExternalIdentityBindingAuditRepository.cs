using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.ExternalConnectors.Repositories;

public sealed class ExternalIdentityBindingAuditRepository : IExternalIdentityBindingAuditRepository
{
    private readonly ISqlSugarClient _db;

    public ExternalIdentityBindingAuditRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task AddAsync(ExternalIdentityBindingAuditLog log, CancellationToken cancellationToken)
        => await _db.Insertable(log).ExecuteCommandAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalIdentityBindingAuditLog>> ListByBindingAsync(TenantId tenantId, long bindingId, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalIdentityBindingAuditLog>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.BindingId == bindingId)
            .OrderBy(x => x.OccurredAt, OrderByType.Desc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExternalIdentityBindingAuditLog>> ListByExternalUserAsync(TenantId tenantId, long providerId, string externalUserId, int take, CancellationToken cancellationToken)
        => await _db.Queryable<ExternalIdentityBindingAuditLog>()
            .Where(x => x.TenantIdValue == tenantId.Value
                && x.ProviderId == providerId
                && x.ExternalUserId == externalUserId)
            .OrderBy(x => x.OccurredAt, OrderByType.Desc)
            .Take(take)
            .ToListAsync(cancellationToken);
}
