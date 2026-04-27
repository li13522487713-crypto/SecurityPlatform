namespace Atlas.Application.Microflows.Runtime.Transactions;

public interface IMicroflowUnitOfWork
{
    string Id { get; }

    IReadOnlyList<MicroflowRuntimeChangedObject> Changes { get; }

    IReadOnlyList<MicroflowRuntimeTransactionOperation> Operations { get; }

    void Stage(MicroflowRuntimeChangedObject change);

    void MarkCommitted(string? reason = null);

    void MarkRolledBack(string? reason = null);

    void Clear();

    MicroflowRuntimeUnitOfWorkSnapshot CreateSnapshot();
}
