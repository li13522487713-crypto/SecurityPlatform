using System.Threading.Channels;
using Atlas.Core.Abstractions;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// In-process background work queue backed by <see cref="Channel{T}"/>.
/// Each enqueued work item is executed in a new DI scope by <see cref="BackgroundWorkQueueProcessor"/>.
/// This replaces the unsafe <c>Task.Run</c> pattern that captures request-scoped services.
/// </summary>
public sealed class BackgroundWorkQueue : IBackgroundWorkQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _channel =
        Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    public void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        if (!_channel.Writer.TryWrite(workItem))
        {
            throw new InvalidOperationException("Background work queue has been completed.");
        }
    }

    internal ChannelReader<Func<IServiceProvider, CancellationToken, Task>> Reader => _channel.Reader;
}
