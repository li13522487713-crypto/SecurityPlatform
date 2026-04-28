using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IModelConfigQueryService
{
    Task<PagedResult<ModelConfigDto>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        string? workspaceId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ModelConfigDto?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ModelConfigDto>> GetAllEnabledAsync(
        TenantId tenantId,
        string? workspaceId,
        CancellationToken cancellationToken);

    Task<ModelConfigStatsDto> GetStatsAsync(
        TenantId tenantId,
        string? keyword,
        string? workspaceId,
        CancellationToken cancellationToken);
}
