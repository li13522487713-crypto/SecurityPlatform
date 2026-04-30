using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Objects;

public interface IDatabaseBackedMicroflowRuntimeObjectStore : IMicroflowRuntimeObjectStore;

public sealed class ModeAwareRuntimeObjectStore : IMicroflowRuntimeObjectStore
{
    private readonly InMemoryRuntimeObjectStore _inMemoryStore;
    private readonly IDatabaseBackedMicroflowRuntimeObjectStore? _databaseBackedStore;

    public ModeAwareRuntimeObjectStore(
        InMemoryRuntimeObjectStore inMemoryStore,
        IDatabaseBackedMicroflowRuntimeObjectStore? databaseBackedStore = null)
    {
        _inMemoryStore = inMemoryStore;
        _databaseBackedStore = databaseBackedStore;
    }

    public Task<MicroflowRuntimeObjectStoreResult> RetrieveAsync(MicroflowRuntimeObjectQuery query, CancellationToken ct)
        => Resolve(query.RuntimeContext?.Mode).RetrieveAsync(query, ct);

    public Task<MicroflowRuntimeObjectStoreResult> CreateAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
        => Resolve(mutation.RuntimeContext?.Mode).CreateAsync(mutation, ct);

    public Task<MicroflowRuntimeObjectStoreResult> ChangeAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
        => Resolve(mutation.RuntimeContext?.Mode).ChangeAsync(mutation, ct);

    public Task<MicroflowRuntimeObjectStoreResult> CommitAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
        => Resolve(mutation.RuntimeContext?.Mode).CommitAsync(mutation, ct);

    public Task<MicroflowRuntimeObjectStoreResult> DeleteAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
        => Resolve(mutation.RuntimeContext?.Mode).DeleteAsync(mutation, ct);

    public Task<MicroflowRuntimeObjectStoreResult> RollbackAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
        => Resolve(mutation.RuntimeContext?.Mode).RollbackAsync(mutation, ct);

    private IMicroflowRuntimeObjectStore Resolve(string? mode)
    {
        if (string.Equals(mode, MicroflowRuntimeExecutionMode.PublishedRun, StringComparison.OrdinalIgnoreCase))
        {
            return _databaseBackedStore is not null
                ? _databaseBackedStore
                : new FailFastRuntimeObjectStore();
        }

        return _inMemoryStore;
    }

    private sealed class FailFastRuntimeObjectStore : IMicroflowRuntimeObjectStore
    {
        public Task<MicroflowRuntimeObjectStoreResult> RetrieveAsync(MicroflowRuntimeObjectQuery query, CancellationToken ct)
            => Task.FromResult(Failed());

        public Task<MicroflowRuntimeObjectStoreResult> CreateAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
            => Task.FromResult(Failed());

        public Task<MicroflowRuntimeObjectStoreResult> ChangeAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
            => Task.FromResult(Failed());

        public Task<MicroflowRuntimeObjectStoreResult> CommitAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
            => Task.FromResult(Failed());

        public Task<MicroflowRuntimeObjectStoreResult> DeleteAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
            => Task.FromResult(Failed());

        public Task<MicroflowRuntimeObjectStoreResult> RollbackAsync(MicroflowRuntimeObjectMutation mutation, CancellationToken ct)
            => Task.FromResult(Failed());

        private static MicroflowRuntimeObjectStoreResult Failed()
            => new()
            {
                Success = false,
                Code = RuntimeErrorCode.RuntimeTransactionRequired,
                Message = "PublishedRun requires a DB-backed runtime object store."
            };
    }
}
