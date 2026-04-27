namespace Atlas.Application.Microflows.Runtime.Transactions;

public sealed class MicroflowUnitOfWork : IMicroflowUnitOfWork
{
    private readonly List<MicroflowRuntimeChangedObject> _changes = [];
    private readonly List<MicroflowRuntimeTransactionOperation> _operations = [];

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public IReadOnlyList<MicroflowRuntimeChangedObject> Changes => _changes;

    public IReadOnlyList<MicroflowRuntimeTransactionOperation> Operations => _operations;

    public void Stage(MicroflowRuntimeChangedObject change)
    {
        _changes.Add(change);
        _operations.Add(new MicroflowRuntimeTransactionOperation
        {
            Operation = change.Operation,
            ChangeId = change.Id,
            ObjectId = change.SourceObjectId,
            ActionId = change.SourceActionId,
            EntityQualifiedName = change.EntityQualifiedName,
            RuntimeObjectId = change.ObjectId,
            Status = change.Status,
            Message = change.Preview
        });
    }

    public void MarkCommitted(string? reason = null)
    {
        for (var index = 0; index < _changes.Count; index++)
        {
            _changes[index] = _changes[index] with { Status = MicroflowRuntimeObjectChangeStatus.Committed };
        }

        _operations.Add(new MicroflowRuntimeTransactionOperation
        {
            Operation = MicroflowRuntimeObjectChangeOperation.Commit,
            Status = MicroflowRuntimeObjectChangeStatus.Committed,
            Reason = reason,
            Message = reason ?? "UnitOfWork committed."
        });
    }

    public void MarkRolledBack(string? reason = null)
    {
        for (var index = 0; index < _changes.Count; index++)
        {
            _changes[index] = _changes[index] with { Status = MicroflowRuntimeObjectChangeStatus.RolledBack };
        }

        _operations.Add(new MicroflowRuntimeTransactionOperation
        {
            Operation = MicroflowRuntimeObjectChangeOperation.Rollback,
            Status = MicroflowRuntimeObjectChangeStatus.RolledBack,
            Reason = reason,
            Message = reason ?? "UnitOfWork rolled back."
        });
    }

    public void Clear()
    {
        _changes.Clear();
        _operations.Clear();
    }

    public MicroflowRuntimeUnitOfWorkSnapshot CreateSnapshot()
        => new()
        {
            Id = Id,
            Changes = _changes.ToArray(),
            Operations = _operations.ToArray()
        };
}
