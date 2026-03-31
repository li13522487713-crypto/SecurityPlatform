using Atlas.Application.DynamicViews.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicViews.Abstractions;

public interface IDynamicViewCommandService
{
    Task<string> CreateAsync(
        TenantId tenantId,
        long userId,
        DynamicViewCreateOrUpdateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long userId,
        string viewKey,
        DynamicViewCreateOrUpdateRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken);

    Task<DynamicViewPublishResultDto> PublishAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string viewKey,
        string? comment,
        CancellationToken cancellationToken);

    Task<DynamicViewPublishResultDto> RollbackAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string viewKey,
        int version,
        string? comment,
        CancellationToken cancellationToken);
}
