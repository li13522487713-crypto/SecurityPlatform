using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IProjectRepository
{
    Task<Project?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<Project?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Project> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        IReadOnlyList<long>? restrictToProjectIds,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Project>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken);
    Task<(IReadOnlyList<Project> Items, int TotalCount)> QueryPagedByUserIdAsync(
        TenantId tenantId,
        long userId,
        int pageIndex,
        int pageSize,
        string? keyword,
        IReadOnlyList<long>? restrictToProjectIds,
        CancellationToken cancellationToken);
    Task AddAsync(Project project, CancellationToken cancellationToken);
    Task UpdateAsync(Project project, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}
