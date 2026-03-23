using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiMemoryService
{
    Task<PagedResult<LongTermMemoryListItem>> GetLongTermMemoriesAsync(
        TenantId tenantId,
        long userId,
        long? agentId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task DeleteLongTermMemoryAsync(
        TenantId tenantId,
        long userId,
        long memoryId,
        CancellationToken cancellationToken);

    Task<int> ClearLongTermMemoriesAsync(
        TenantId tenantId,
        long userId,
        long? agentId,
        CancellationToken cancellationToken);
}
