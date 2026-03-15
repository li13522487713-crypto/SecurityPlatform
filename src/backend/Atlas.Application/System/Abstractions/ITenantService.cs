using Atlas.Application.System.Models;
using Atlas.Core.Models;

namespace Atlas.Application.System.Abstractions;

public interface ITenantService
{
    Task<long> CreateAsync(long userId, TenantCreateRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(long userId, TenantUpdateRequest request, CancellationToken cancellationToken = default);
    Task ToggleStatusAsync(long userId, long id, bool isActive, CancellationToken cancellationToken = default);
    Task DeleteAsync(long userId, long id, CancellationToken cancellationToken = default);
    Task<TenantDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<PagedResult<TenantDto>> GetPagedAsync(TenantQueryRequest request, CancellationToken cancellationToken = default);
}
