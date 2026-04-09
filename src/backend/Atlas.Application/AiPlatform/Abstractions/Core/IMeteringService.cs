using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IMeteringService
{
    Task RecordLlmUsageAsync(
        TenantId tenantId,
        LlmUsageRecordCreateRequest request,
        CancellationToken cancellationToken = default);
}
