using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAgentQueryService
{
    Task<PagedResult<AgentListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        string? status,
        long? workspaceId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<AgentDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}
