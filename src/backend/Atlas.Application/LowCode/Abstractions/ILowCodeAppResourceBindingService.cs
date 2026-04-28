using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

/// <summary>
/// 低代码应用资源绑定服务。
/// 复用 AiAppResourceBinding 存储表，但 lowcode 路由下的 appId 始终表示 AppDefinition.Id。
/// </summary>
public interface ILowCodeAppResourceBindingService
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
