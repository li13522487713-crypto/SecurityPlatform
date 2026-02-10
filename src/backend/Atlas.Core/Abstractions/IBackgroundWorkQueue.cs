namespace Atlas.Core.Abstractions;

/// <summary>
/// Represents a work item to be executed in the background with a dedicated DI scope.
/// This replaces the unsafe fire-and-forget Task.Run pattern that captures scoped services.
/// </summary>
public interface IBackgroundWorkQueue
{
    /// <summary>
    /// Enqueues a work item to be executed in the background.
    /// The <paramref name="workItem"/> delegate receives an <see cref="IServiceProvider"/>
    /// scoped to that individual background execution, and a <see cref="CancellationToken"/>
    /// tied to application shutdown.
    /// </summary>
    void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem);
}
