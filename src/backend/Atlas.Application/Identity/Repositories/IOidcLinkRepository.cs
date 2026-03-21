using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IOidcLinkRepository
{
    Task<OidcLink?> FindByProviderSubAsync(TenantId tenantId, string providerId, string externalSub, CancellationToken cancellationToken);
    Task<OidcLink?> FindByEmailAsync(TenantId tenantId, string providerId, string email, CancellationToken cancellationToken);
    Task<IReadOnlyList<OidcLink>> GetByUserIdAsync(TenantId tenantId, long userId, CancellationToken cancellationToken);
}