using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IRagExperimentService
{
    Task<RagExperimentDecision> ResolveDecisionAsync(
        TenantId tenantId,
        string query,
        CancellationToken cancellationToken = default);

    Task<long> RecordRunAsync(
        TenantId tenantId,
        RagExperimentRunCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<long> RecordShadowComparisonAsync(
        TenantId tenantId,
        long mainRunId,
        long shadowRunId,
        IReadOnlyList<RagSearchResult> mainResults,
        IReadOnlyList<RagSearchResult> shadowResults,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RagExperimentRunDto>> GetRecentRunsAsync(
        TenantId tenantId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RagShadowComparisonDto>> GetRecentComparisonsAsync(
        TenantId tenantId,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
