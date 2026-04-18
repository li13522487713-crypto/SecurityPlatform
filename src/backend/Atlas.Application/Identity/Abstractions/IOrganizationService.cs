using Atlas.Application.Identity.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IOrganizationService
{
    Task<IReadOnlyList<OrganizationDto>> ListAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<OrganizationDto?> GetByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken);

    Task<OrganizationDto> CreateAsync(TenantId tenantId, long createdBy, OrganizationCreateRequest request, CancellationToken cancellationToken);

    Task UpdateAsync(TenantId tenantId, long id, long updatedBy, OrganizationUpdateRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    /// <summary>幂等：返回租户的默认组织；不存在则创建一个 code=default 的组织。</summary>
    Task<OrganizationDto> GetOrCreateDefaultAsync(TenantId tenantId, long createdBy, CancellationToken cancellationToken);
}
