using Atlas.Application.System.Models;

namespace Atlas.Application.System.Abstractions;

public interface ITenantDataSourceService
{
    Task<IReadOnlyList<TenantDataSourceDto>> QueryAllAsync(string tenantIdValue, CancellationToken cancellationToken = default);

    Task<TenantDataSourceDto?> GetByIdAsync(string tenantIdValue, long id, CancellationToken cancellationToken = default);

    Task<long> CreateAsync(string tenantIdValue, TenantDataSourceCreateRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(string tenantIdValue, long id, TenantDataSourceUpdateRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string tenantIdValue, long id, CancellationToken cancellationToken = default);

    Task<TestConnectionResult> TestConnectionAsync(TestConnectionRequest request, CancellationToken cancellationToken = default);

    Task<TestConnectionResult> TestConnectionByDataSourceIdAsync(string tenantId, long id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DataSourceConsumerItem>> GetConsumersAsync(string tenantIdValue, long dataSourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DataSourceOrphanItem>> GetOrphansAsync(string tenantIdValue, CancellationToken cancellationToken = default);
}
