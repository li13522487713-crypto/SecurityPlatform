using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Core;

public interface IPublishOrchestrator
{
    Task<PublishOrchestrationResult> PublishAsync(
        TenantId tenantId,
        long userId,
        PublishEnvelope envelope,
        CancellationToken cancellationToken);
}
