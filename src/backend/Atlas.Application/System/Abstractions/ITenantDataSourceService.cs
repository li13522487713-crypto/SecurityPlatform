using Atlas.Application.System.Models;

namespace Atlas.Application.System.Abstractions;

public interface ITenantDataSourceService
{
    Task<IReadOnlyList<TenantDataSourceDto>> QueryAllAsync(CancellationToken cancellationToken = default);

    Task<TenantDataSourceDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<long> CreateAsync(TenantDataSourceCreateRequest request, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(long id, TenantDataSourceUpdateRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long id, CancellationToken cancellationToken = default);

    Task<TestConnectionResult> TestConnectionAsync(TestConnectionRequest request, CancellationToken cancellationToken = default);

    Task<TestConnectionResult> TestConnectionByDataSourceIdAsync(string tenantId, long id, CancellationToken cancellationToken = default);
}
