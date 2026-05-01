using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Metadata;
using Atlas.Application.Microflows.Runtime.Security;

namespace Atlas.Application.Microflows.Runtime.Objects;

public sealed class DomainModelRuntimeObjectStore : IMicroflowRuntimeObjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMicroflowEntityAccessService _entityAccessService;
    private readonly IDatabaseBackedMicroflowRuntimeObjectStore? _databaseStore;

    public DomainModelRuntimeObjectStore(
        IMicroflowEntityAccessService entityAccessService,
        IDatabaseBackedMicroflowRuntimeObjectStore? databaseStore = null)
    {
        _entityAccessService = entityAccessService;
        _databaseStore = databaseStore;
    }

    public async Task<MicroflowRuntimeObjectStoreResult> RetrieveAsync(MicroflowRuntimeObjectQuery query, CancellationToken ct)
    {
        var access = await _entityAccessService.CanReadAsync(Security(query), Entity(query.EntityType), ct);
        if (!access.Allowed)
        {
            return Denied(access.Reason);
        }

        if (string.Equals(query.RuntimeContext?.Mode, MicroflowRuntimeExecutionMode.TestRun, StringComparison.OrdinalIgnoreCase))
        {
            return new MicroflowRuntimeObjectStoreResult
            {
                Success = true,
                Items = Array.Empty<JsonElement>(),
                Value = JsonSerializer.SerializeToElement(new { items = Array.Empty<object>() }, JsonOptions),
                Message = "Dry-run retrieve returned an empty in-memory result."
            };
        }

        return access.Allowed
            ? await ResolveStore().RetrieveAsync(query, ct)
            : Denied(access.Reason);
    }

    public async Task<MicroflowRuntimeObjectStoreResult> CreateAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        var access = await _entityAccessService.CanCreateAsync(Security(mutation), Entity(mutation.EntityType), ct);
        return access.Allowed
            ? await ResolveStore().CreateAsync(mutation, ct)
            : Denied(access.Reason);
    }

    public async Task<MicroflowRuntimeObjectStoreResult> ChangeAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        var access = await _entityAccessService.CanUpdateAsync(Security(mutation), Entity(mutation.EntityType), ct);
        return access.Allowed
            ? await ResolveStore().ChangeAsync(mutation, ct)
            : Denied(access.Reason);
    }

    public async Task<MicroflowRuntimeObjectStoreResult> CommitAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        var access = await _entityAccessService.CanUpdateAsync(Security(mutation), Entity(mutation.EntityType), ct);
        if (!access.Allowed)
        {
            return Denied(access.Reason);
        }

        if (mutation.DryRun)
        {
            return new MicroflowRuntimeObjectStoreResult
            {
                Success = true,
                Value = mutation.Value,
                Message = "Dry-run commit accepted without persistent write."
            };
        }

        return await ResolveStore().CommitAsync(mutation, ct);
    }

    public async Task<MicroflowRuntimeObjectStoreResult> DeleteAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
    {
        var access = await _entityAccessService.CanDeleteAsync(Security(mutation), Entity(mutation.EntityType), ct);
        if (!access.Allowed)
        {
            return Denied(access.Reason);
        }

        if (mutation.DryRun)
        {
            return new MicroflowRuntimeObjectStoreResult
            {
                Success = true,
                Value = mutation.Value,
                Message = "Dry-run delete accepted without persistent write."
            };
        }

        return await ResolveStore().DeleteAsync(mutation, ct);
    }

    public Task<MicroflowRuntimeObjectStoreResult> RollbackAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
        => mutation.DryRun
            ? Task.FromResult(new MicroflowRuntimeObjectStoreResult
            {
                Success = true,
                Value = mutation.Value,
                Message = "Dry-run rollback accepted without persistent write."
            })
            : ResolveStore().RollbackAsync(mutation, ct);

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

    private IDatabaseBackedMicroflowRuntimeObjectStore ResolveStore()
        => _databaseStore ?? throw new InvalidOperationException("DB-backed runtime object store is not registered.");
}
