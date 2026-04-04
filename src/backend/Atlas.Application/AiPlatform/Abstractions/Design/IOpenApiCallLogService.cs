using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IOpenApiCallLogService
{
    Task WriteAsync(
        TenantId tenantId,
        OpenApiCallLogCreateRequest request,
        CancellationToken cancellationToken);

    Task<OpenApiCallStatsSummary> GetSummaryAsync(
        TenantId tenantId,
        long? projectId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken);
}
