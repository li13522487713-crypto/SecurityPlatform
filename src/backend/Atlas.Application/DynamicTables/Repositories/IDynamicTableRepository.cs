using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;

namespace Atlas.Application.DynamicTables.Repositories;

public interface IDynamicTableRepository
{
    Task<DynamicTable?> FindByKeyAsync(
        TenantId tenantId,
        string tableKey,
        long? appId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DynamicTable>> QueryByKeysAsync(
        TenantId tenantId,
        IReadOnlyList<string> tableKeys,
        long? appId,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<DynamicTable> Items, int TotalCount)> QueryPageAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        string? keyword,
        long? appId,
        CancellationToken cancellationToken);

    Task AddAsync(DynamicTable table, CancellationToken cancellationToken);

    Task UpdateAsync(DynamicTable table, CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);
}
