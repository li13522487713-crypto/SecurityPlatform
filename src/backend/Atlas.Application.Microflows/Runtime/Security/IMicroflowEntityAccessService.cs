using Atlas.Application.Microflows.Runtime.Metadata;

namespace Atlas.Application.Microflows.Runtime.Security;

public interface IMicroflowEntityAccessService
{
    Task<MicroflowEntityAccessDecision> CanReadAsync(
        MicroflowRuntimeSecurityContext security,
        MicroflowResolvedEntity entity,
        CancellationToken ct);

    Task<MicroflowEntityAccessDecision> CanCreateAsync(
        MicroflowRuntimeSecurityContext security,
        MicroflowResolvedEntity entity,
        CancellationToken ct);

    Task<MicroflowEntityAccessDecision> CanUpdateAsync(
        MicroflowRuntimeSecurityContext security,
        MicroflowResolvedEntity entity,
        CancellationToken ct);

    Task<MicroflowEntityAccessDecision> CanDeleteAsync(
        MicroflowRuntimeSecurityContext security,
        MicroflowResolvedEntity entity,
        CancellationToken ct);

    Task<MicroflowEntityAccessDecision> CanExecuteMicroflowAsync(
        MicroflowRuntimeSecurityContext security,
        MicroflowResolvedMicroflowRef microflow,
        CancellationToken ct);
}
