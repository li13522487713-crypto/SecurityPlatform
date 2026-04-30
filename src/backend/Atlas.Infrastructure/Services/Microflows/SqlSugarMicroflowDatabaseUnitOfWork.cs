using Atlas.Application.Microflows.Runtime.Transactions;

namespace Atlas.Infrastructure.Services.Microflows;

public sealed class SqlSugarMicroflowDatabaseUnitOfWork : IMicroflowDatabaseUnitOfWork
{
    private readonly IMicroflowRuntimeDbSession _session;

    public SqlSugarMicroflowDatabaseUnitOfWork(IMicroflowRuntimeDbSession session)
    {
        _session = session;
    }

    public Task BeginAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _session.Begin();
        return Task.CompletedTask;
    }

    public Task CommitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _session.Commit();
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _session.Rollback();
        return Task.CompletedTask;
    }

    public Task CreateSavepointAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = name;
        return Task.CompletedTask;
    }

    public Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = name;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
