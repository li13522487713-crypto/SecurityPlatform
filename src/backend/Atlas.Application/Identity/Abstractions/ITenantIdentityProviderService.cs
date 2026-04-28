using Atlas.Application.Identity.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface ITenantIdentityProviderService
{
    Task<IReadOnlyList<TenantIdentityProviderDto>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<TenantIdentityProviderDto?> GetByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken);

    Task<TenantIdentityProviderDto> CreateAsync(TenantId tenantId, long createdBy, TenantIdentityProviderCreateRequest request, CancellationToken cancellationToken);

    Task UpdateAsync(TenantId tenantId, long id, long updatedBy, TenantIdentityProviderUpdateRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}
