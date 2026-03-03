using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface ILowCodeAppCommandService
{
    Task<long> CreateAsync(TenantId tenantId, long userId, LowCodeAppCreateRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantId tenantId, long userId, long id, LowCodeAppUpdateRequest request, CancellationToken cancellationToken = default);
    Task PublishAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default);
    Task DisableAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default);
    Task EnableAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default);
    Task<LowCodeAppImportResult> ImportAsync(TenantId tenantId, long userId, LowCodeAppImportRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default);
}
