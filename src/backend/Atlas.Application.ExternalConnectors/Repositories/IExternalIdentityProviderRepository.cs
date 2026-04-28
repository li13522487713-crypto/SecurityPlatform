using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Application.ExternalConnectors.Repositories;

public interface IExternalIdentityProviderRepository
{
    Task AddAsync(ExternalIdentityProvider entity, CancellationToken cancellationToken);

    Task UpdateAsync(ExternalIdentityProvider entity, CancellationToken cancellationToken);

    Task<ExternalIdentityProvider?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<ExternalIdentityProvider?> GetByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalIdentityProvider>> ListAsync(TenantId tenantId, ConnectorProviderType? type, bool includeDisabled, CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}
