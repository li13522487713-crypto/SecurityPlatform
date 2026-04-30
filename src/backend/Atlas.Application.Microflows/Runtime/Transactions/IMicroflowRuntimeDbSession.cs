using Atlas.Application.Microflows.Contracts;

namespace Atlas.Application.Microflows.Runtime.Transactions;

public interface IMicroflowRuntimeDbSession
{
    string Id { get; }

    bool HasActiveTransaction { get; }

    bool OwnsLifecycle { get; }

    void Begin(string? transactionId = null);

    void Commit(string? transactionId = null, string? reason = null);

    void Rollback(string? transactionId = null, string? reason = null);

    T? GetNativeSession<T>() where T : class;
}

public interface IMicroflowRuntimeDbSessionFactory
{
    IMicroflowRuntimeDbSession? Create(
        MicroflowRequestContext requestContext,
        IMicroflowRuntimeDbSession? parentSession,
        string transactionBoundary);
}
