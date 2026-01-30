using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Repositories;

public interface IPositionRepository
{
    Task<Position?> FindByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
    Task<Position?> FindByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Position> Items, int TotalCount)> QueryPageAsync(
        int pageIndex,
        int pageSize,
        string? keyword,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Position>> QueryByIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Position>> QueryAllAsync(TenantId tenantId, CancellationToken cancellationToken);
    Task AddAsync(Position position, CancellationToken cancellationToken);
    Task UpdateAsync(Position position, CancellationToken cancellationToken);
    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}
