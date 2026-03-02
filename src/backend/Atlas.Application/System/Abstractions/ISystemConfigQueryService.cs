using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Abstractions;

public interface ISystemConfigQueryService
{
    Task<PagedResult<SystemConfigDto>> GetSystemConfigsPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<SystemConfigDto?> GetByKeyAsync(
        TenantId tenantId,
        string configKey,
        CancellationToken cancellationToken);
}
