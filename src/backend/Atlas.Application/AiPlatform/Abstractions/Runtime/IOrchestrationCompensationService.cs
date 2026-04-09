using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IOrchestrationCompensationService
{
    Task<IReadOnlyList<OrchestrationExecutionTraceStep>> CompensateAsync(
        TenantId tenantId,
        long planId,
        string executionId,
        IReadOnlyList<OrchestrationExecutionTraceStep> completedSteps,
        CancellationToken cancellationToken = default);
}
