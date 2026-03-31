using Atlas.Application.DynamicViews.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicViews.Abstractions;

public interface IDynamicDeleteCheckService
{
    Task<DeleteCheckResultDto> CheckTableDeleteAsync(
        TenantId tenantId,
        long? appId,
        string tableKey,
        CancellationToken cancellationToken);

    Task<DeleteCheckResultDto> CheckViewDeleteAsync(
        TenantId tenantId,
        long? appId,
        string viewKey,
        CancellationToken cancellationToken);
}
