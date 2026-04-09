using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IRagFeedbackService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        RagFeedbackCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RagFeedbackDto>> GetByQueryIdAsync(
        TenantId tenantId,
        string queryId,
        CancellationToken cancellationToken = default);
}
