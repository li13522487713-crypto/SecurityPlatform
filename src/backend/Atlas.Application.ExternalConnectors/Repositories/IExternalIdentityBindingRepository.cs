using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Application.ExternalConnectors.Repositories;

public interface IExternalIdentityBindingRepository
{
    Task AddAsync(ExternalIdentityBinding entity, CancellationToken cancellationToken);

    Task UpdateAsync(ExternalIdentityBinding entity, CancellationToken cancellationToken);

    Task<ExternalIdentityBinding?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<ExternalIdentityBinding?> GetByExternalUserIdAsync(TenantId tenantId, long providerId, string externalUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalIdentityBinding>> GetByLocalUserIdAsync(TenantId tenantId, long localUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalIdentityBinding>> ListConflictsAsync(TenantId tenantId, long providerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalIdentityBinding>> ListByProviderAsync(TenantId tenantId, long providerId, IdentityBindingStatus? status, int skip, int take, CancellationToken cancellationToken);

    Task<int> CountByProviderAsync(TenantId tenantId, long providerId, IdentityBindingStatus? status, CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}
