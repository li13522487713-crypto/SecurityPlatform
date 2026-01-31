using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IAppConfigRepository
{
    Task<AppConfig?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<AppConfig?> FindByAppIdAsync(TenantId tenantId, string appId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<AppConfig> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken);
    Task AddAsync(AppConfig appConfig, CancellationToken cancellationToken);
    Task UpdateAsync(AppConfig appConfig, CancellationToken cancellationToken);
}
