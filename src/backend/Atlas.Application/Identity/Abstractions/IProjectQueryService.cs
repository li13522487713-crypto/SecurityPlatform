using Atlas.Application.Identity.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface IProjectQueryService
{
    Task<PagedResult<ProjectListItem>> QueryProjectsAsync(
        PagedRequest request,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<ProjectDetail?> GetDetailAsync(long id, TenantId tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectListItem>> QueryMyProjectsAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);
}
