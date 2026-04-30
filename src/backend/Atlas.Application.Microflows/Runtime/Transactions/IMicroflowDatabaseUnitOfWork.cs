namespace Atlas.Application.Microflows.Runtime.Transactions;

public interface IMicroflowDatabaseUnitOfWork : IAsyncDisposable
{
    Task BeginAsync(CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);

    Task CreateSavepointAsync(string name, CancellationToken cancellationToken);

    Task RollbackToSavepointAsync(string name, CancellationToken cancellationToken);
}
