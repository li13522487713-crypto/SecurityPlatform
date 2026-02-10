namespace Atlas.Core.Abstractions;

/// <summary>
/// Provides transactional unit of work for grouping multiple repository operations.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Executes the given async action inside a database transaction.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);
}
