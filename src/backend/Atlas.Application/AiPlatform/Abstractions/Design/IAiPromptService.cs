using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiPromptService
{
    Task<PagedResult<AiPromptTemplateListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        string? category,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AiPromptTemplateDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<long> CreateAsync(
        TenantId tenantId,
        AiPromptTemplateCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long id,
        AiPromptTemplateUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);
}
