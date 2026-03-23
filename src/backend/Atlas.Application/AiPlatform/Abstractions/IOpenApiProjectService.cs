using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IOpenApiProjectService
{
    Task<PagedResult<OpenApiProjectListItem>> GetPagedAsync(
        TenantId tenantId,
        long createdByUserId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OpenApiProjectCreateResult> CreateAsync(
        TenantId tenantId,
        long createdByUserId,
        OpenApiProjectCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long createdByUserId,
        long projectId,
        OpenApiProjectUpdateRequest request,
        CancellationToken cancellationToken);

    Task<OpenApiProjectRotateSecretResult> RotateSecretAsync(
        TenantId tenantId,
        long createdByUserId,
        long projectId,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long createdByUserId,
        long projectId,
        CancellationToken cancellationToken);

    Task<OpenApiProjectTokenExchangeResult> ExchangeTokenAsync(
        TenantId tenantId,
        OpenApiProjectTokenExchangeRequest request,
        CancellationToken cancellationToken);
}
