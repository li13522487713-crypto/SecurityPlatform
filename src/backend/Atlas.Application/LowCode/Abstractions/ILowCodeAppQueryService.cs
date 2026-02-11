using Atlas.Application.LowCode.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface ILowCodeAppQueryService
{
    Task<PagedResult<LowCodeAppListItem>> QueryAsync(
        PagedRequest request, TenantId tenantId, string? category = null,
        CancellationToken cancellationToken = default);

    Task<LowCodeAppDetail?> GetByIdAsync(
        TenantId tenantId, long id, CancellationToken cancellationToken = default);

    Task<LowCodeAppDetail?> GetByKeyAsync(
        TenantId tenantId, string appKey, CancellationToken cancellationToken = default);
}
