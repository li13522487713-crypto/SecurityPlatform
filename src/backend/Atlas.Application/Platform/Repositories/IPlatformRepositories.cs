using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;

namespace Atlas.Application.Platform.Repositories;

public interface IAppManifestRepository
{
    Task<PagedResult<AppManifest>> QueryAsync(TenantId tenantId, int pageIndex, int pageSize, string? keyword, CancellationToken cancellationToken = default);
    Task<AppManifest?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByKeyAsync(TenantId tenantId, string appKey, CancellationToken cancellationToken = default);
    Task InsertAsync(AppManifest entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(AppManifest entity, CancellationToken cancellationToken = default);
}

public interface IAppReleaseRepository
{
    Task<PagedResult<AppRelease>> QueryAsync(TenantId tenantId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);
    Task<AppRelease?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<int> GetLatestVersionAsync(TenantId tenantId, long manifestId, CancellationToken cancellationToken = default);
    Task InsertAsync(AppRelease entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(AppRelease entity, CancellationToken cancellationToken = default);
}

public interface IRuntimeRouteRepository
{
    Task<RuntimeRoute?> GetByAppAndPageKeyAsync(TenantId tenantId, string appKey, string pageKey, CancellationToken cancellationToken = default);
    Task UpsertAsync(RuntimeRoute route, CancellationToken cancellationToken = default);
}
