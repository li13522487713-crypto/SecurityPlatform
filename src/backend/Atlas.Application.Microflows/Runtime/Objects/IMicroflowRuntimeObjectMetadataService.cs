namespace Atlas.Application.Microflows.Runtime.Objects;

public interface IMicroflowRuntimeObjectMetadataService
{
    Task<MicroflowObjectOperationPlan> BuildRetrievePlanAsync(MicroflowObjectOperationRequest request, CancellationToken ct);

    Task<MicroflowObjectOperationPlan> BuildCreatePlanAsync(MicroflowObjectOperationRequest request, CancellationToken ct);

    Task<MicroflowObjectOperationPlan> BuildChangeMembersPlanAsync(MicroflowObjectOperationRequest request, CancellationToken ct);

    Task<MicroflowObjectOperationPlan> BuildCommitPlanAsync(MicroflowObjectOperationRequest request, CancellationToken ct);

    Task<MicroflowObjectOperationPlan> BuildDeletePlanAsync(MicroflowObjectOperationRequest request, CancellationToken ct);
}
