using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;

namespace Atlas.Application.ExternalConnectors.Repositories;

public interface IExternalIdentityBindingAuditRepository
{
    Task AddAsync(ExternalIdentityBindingAuditLog log, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalIdentityBindingAuditLog>> ListByBindingAsync(TenantId tenantId, long bindingId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalIdentityBindingAuditLog>> ListByExternalUserAsync(TenantId tenantId, long providerId, string externalUserId, int take, CancellationToken cancellationToken);
}
