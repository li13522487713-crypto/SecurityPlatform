using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface ILowCodePageCommandService
{
    Task<long> CreateAsync(TenantId tenantId, long userId, long appId, LowCodePageCreateRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantId tenantId, long userId, long id, LowCodePageUpdateRequest request, CancellationToken cancellationToken = default);
    Task UpdateSchemaAsync(TenantId tenantId, long userId, long id, LowCodePageSchemaUpdateRequest request, CancellationToken cancellationToken = default);
    Task PublishAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default);
    Task UnpublishAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default);
    Task RollbackAsync(TenantId tenantId, long userId, long id, long versionId, CancellationToken cancellationToken = default);
    Task DeleteAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default);
}
