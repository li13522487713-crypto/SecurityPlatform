using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IPersonalAccessTokenService
{
    Task<PagedResult<PersonalAccessTokenListItem>> GetPagedAsync(
        TenantId tenantId,
        long createdByUserId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PersonalAccessTokenCreateResult> CreateAsync(
        TenantId tenantId,
        long createdByUserId,
        PersonalAccessTokenCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long createdByUserId,
        long tokenId,
        PersonalAccessTokenUpdateRequest request,
        CancellationToken cancellationToken);

    Task RevokeAsync(
        TenantId tenantId,
        long createdByUserId,
        long tokenId,
        CancellationToken cancellationToken);

    Task<PersonalAccessTokenValidateResult> ValidateAsync(
        TenantId tenantId,
        string rawToken,
        CancellationToken cancellationToken);
}
