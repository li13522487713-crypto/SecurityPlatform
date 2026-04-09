using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class OrchestrationCompensationService : IOrchestrationCompensationService
{
    public Task<IReadOnlyList<OrchestrationExecutionTraceStep>> CompensateAsync(
        TenantId tenantId,
        long planId,
        string executionId,
        IReadOnlyList<OrchestrationExecutionTraceStep> completedSteps,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        _ = planId;
        _ = executionId;
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTimeOffset.UtcNow;
        var compensated = completedSteps
            .Where(step => string.Equals(step.Status, "Success", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(step => step.StartedAt)
            .Select(step => new OrchestrationExecutionTraceStep(
                step.NodeId,
                step.NodeType,
                NodeLifecycleStateMachine.ToStatus(NodeLifecycleState.Compensated),
                step.Attempt,
                0,
                null,
                now,
                now))
            .ToArray();
        return Task.FromResult<IReadOnlyList<OrchestrationExecutionTraceStep>>(compensated);
    }
}
