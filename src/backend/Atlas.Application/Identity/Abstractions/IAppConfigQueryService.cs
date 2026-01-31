using Atlas.Application.Identity.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IAppConfigQueryService
{
    Task<PagedResult<AppConfigListItem>> QueryAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<AppConfigDetail?> GetDetailAsync(long id, TenantId tenantId, CancellationToken cancellationToken);

    Task<AppConfigDetail?> GetByAppIdAsync(string appId, TenantId tenantId, CancellationToken cancellationToken);
}
