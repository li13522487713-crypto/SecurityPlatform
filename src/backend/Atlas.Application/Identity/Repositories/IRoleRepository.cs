using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IRoleRepository
{
    Task<Role?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<Role?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Role> Items, int TotalCount)> QueryPageAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Role>> QueryByIdsAsync(TenantId tenantId, IReadOnlyList<long> ids, CancellationToken cancellationToken);
    Task AddAsync(Role role, CancellationToken cancellationToken);
    Task UpdateAsync(Role role, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}
