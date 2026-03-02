using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;

namespace Atlas.Application.LowCode.Abstractions;

public interface ILowCodeAppRepository
{
    Task<LowCodeApp?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default);
    Task<LowCodeApp?> GetByKeyAsync(TenantId tenantId, string appKey, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<LowCodeApp> Items, int Total)> GetPagedAsync(
        TenantId tenantId, int pageIndex, int pageSize, string? keyword, string? category,
        CancellationToken cancellationToken = default);
    Task InsertAsync(LowCodeApp entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(LowCodeApp entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByKeyAsync(TenantId tenantId, string appKey, long? excludeId = null, CancellationToken cancellationToken = default);
}
