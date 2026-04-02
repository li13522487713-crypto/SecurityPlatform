using Atlas.Core.Tenancy;

namespace Atlas.Application.LogicFlow.Flows.Abstractions;

public interface IExecutionStateService
{
    Task<long> StartExecutionAsync(
        long flowDefId,
        string? inputJson,
        TenantId tenantId,
        string userId,
        CancellationToken cancellationToken);

    Task CompleteExecutionAsync(long executionId, string? outputJson, CancellationToken cancellationToken);

    Task FailExecutionAsync(long executionId, string errorMessage, CancellationToken cancellationToken);

    Task CancelExecutionAsync(long executionId, CancellationToken cancellationToken);

    Task<long> StartNodeRunAsync(
        long executionId,
        string nodeKey,
        string nodeTypeKey,
        string? inputJson,
        CancellationToken cancellationToken);

    Task CompleteNodeRunAsync(long nodeRunId, string? outputJson, CancellationToken cancellationToken);

    Task FailNodeRunAsync(long nodeRunId, string errorMessage, bool canRetry, CancellationToken cancellationToken);
}
