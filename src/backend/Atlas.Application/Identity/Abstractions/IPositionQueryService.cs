using Atlas.Application.Identity.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IPositionQueryService
{
    Task<PagedResult<PositionListItem>> QueryPositionsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<PositionDetail?> GetDetailAsync(long id, TenantId tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PositionListItem>> QueryAllAsync(
        TenantId tenantId,
        CancellationToken cancellationToken);
}
