using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiVariableService
{
    Task<PagedResult<AiVariableListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        AiVariableScope? scope,
        long? scopeId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AiVariableDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<long> CreateAsync(
        TenantId tenantId,
        AiVariableCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long id,
        AiVariableUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AiSystemVariableDefinition>> GetSystemVariableDefinitionsAsync(
        CancellationToken cancellationToken);
}
