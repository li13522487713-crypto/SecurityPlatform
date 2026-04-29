using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Metadata;
using Atlas.Application.Microflows.Runtime.Security;

namespace Atlas.Application.Microflows.Runtime.Objects;

public sealed class DomainModelRuntimeObjectStore : IMicroflowRuntimeObjectStore
{
    private readonly IMicroflowEntityAccessService _entityAccessService;
    private readonly InMemoryRuntimeObjectStore _fallbackStore = new();

    public DomainModelRuntimeObjectStore(IMicroflowEntityAccessService entityAccessService)
    {
        _entityAccessService = entityAccessService;
    }

    public async Task<MicroflowRuntimeObjectStoreResult> RetrieveAsync(MicroflowRuntimeObjectQuery query, CancellationToken ct)
    {
        var access = await _entityAccessService.CanReadAsync(Security(query), Entity(query.EntityType), ct);
        return access.Allowed
            ? await _fallbackStore.RetrieveAsync(query, ct)
            : Denied(access.Reason);
    }

    public async Task<MicroflowRuntimeObjectStoreResult> CreateAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        var access = await _entityAccessService.CanCreateAsync(Security(mutation), Entity(mutation.EntityType), ct);
        return access.Allowed
            ? await _fallbackStore.CreateAsync(mutation, ct)
            : Denied(access.Reason);
    }

    public async Task<MicroflowRuntimeObjectStoreResult> ChangeAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        var access = await _entityAccessService.CanUpdateAsync(Security(mutation), Entity(mutation.EntityType), ct);
        return access.Allowed
            ? await _fallbackStore.ChangeAsync(mutation, ct)
            : Denied(access.Reason);
    }

    public async Task<MicroflowRuntimeObjectStoreResult> CommitAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        var access = await _entityAccessService.CanUpdateAsync(Security(mutation), Entity(mutation.EntityType), ct);
        return access.Allowed
            ? await _fallbackStore.CommitAsync(mutation with { DryRun = false }, ct)
            : Denied(access.Reason);
    }

    public async Task<MicroflowRuntimeObjectStoreResult> DeleteAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        var access = await _entityAccessService.CanDeleteAsync(Security(mutation), Entity(mutation.EntityType), ct);
        return access.Allowed
            ? await _fallbackStore.DeleteAsync(mutation, ct)
            : Denied(access.Reason);
    }

    public Task<MicroflowRuntimeObjectStoreResult> RollbackAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
        => _fallbackStore.RollbackAsync(mutation, ct);

    private static MicroflowRuntimeSecurityContext Security(MicroflowRuntimeObjectQuery query)
        => new()
        {
            TenantId = query.TenantId,
            WorkspaceId = query.WorkspaceId,
            ApplyEntityAccess = true
        };

    private static MicroflowRuntimeSecurityContext Security(MicroflowRuntimeObjectMutation mutation)
        => new()
        {
            TenantId = mutation.TenantId,
            WorkspaceId = mutation.WorkspaceId,
            ApplyEntityAccess = true
        };

    private static MicroflowResolvedEntity Entity(string entityType)
        => new()
        {
            Found = !string.IsNullOrWhiteSpace(entityType),
            QualifiedName = entityType
        };

    private static MicroflowRuntimeObjectStoreResult Denied(string reason)
        => new()
        {
            Success = false,
            Code = RuntimeErrorCode.RuntimeEntityAccessDenied,
            Message = reason
        };
}
