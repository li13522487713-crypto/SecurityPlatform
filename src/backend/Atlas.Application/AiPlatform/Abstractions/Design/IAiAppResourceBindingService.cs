using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

/// <summary>App 资源绑定（K5/X1）。</summary>
public interface IAiAppResourceBindingService
{
    Task<IReadOnlyList<AiAppResourceBindingDto>> ListByAppAsync(
        TenantId tenantId,
        long appId,
        string? resourceType,
        CancellationToken cancellationToken);

    Task<long> BindAsync(
        TenantId tenantId,
        long appId,
        AiAppResourceBindingCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long appId,
        long bindingId,
        AiAppResourceBindingUpdateRequest request,
        CancellationToken cancellationToken);

    Task UnbindAsync(
        TenantId tenantId,
        long appId,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken);
}
