using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IModelConvergenceService
{
    Task<ModelConvergenceResponse> AnalyzeAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default);
}
