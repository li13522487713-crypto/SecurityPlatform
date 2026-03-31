using Atlas.Application.DynamicViews.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicViews.Abstractions;

public interface IDynamicTransformJobService
{
    Task<IReadOnlyList<DynamicTransformJobDto>> ListAsync(
        TenantId tenantId,
        long? appId,
        CancellationToken cancellationToken);

    Task<DynamicTransformJobDto> CreateAsync(
        TenantId tenantId,
        long userId,
        DynamicTransformJobCreateRequest request,
        CancellationToken cancellationToken);

    Task<DynamicTransformJobDto?> GetAsync(
        TenantId tenantId,
        long? appId,
        string jobKey,
        CancellationToken cancellationToken);

    Task<DynamicTransformJobDto> UpdateAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string jobKey,
        DynamicTransformJobUpdateRequest request,
        CancellationToken cancellationToken);

    Task<DynamicTransformExecutionDto> RunAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string jobKey,
        CancellationToken cancellationToken);

    Task<DynamicTransformJobDto> PauseAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string jobKey,
        CancellationToken cancellationToken);

    Task<DynamicTransformJobDto> ResumeAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string jobKey,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long? appId,
        string jobKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DynamicTransformExecutionDto>> GetHistoryAsync(
        TenantId tenantId,
        long? appId,
        PagedRequest request,
        string jobKey,
        CancellationToken cancellationToken);

    Task<DynamicTransformExecutionDto?> GetExecutionAsync(
        TenantId tenantId,
        long? appId,
        string jobKey,
        string executionId,
        CancellationToken cancellationToken);
}
