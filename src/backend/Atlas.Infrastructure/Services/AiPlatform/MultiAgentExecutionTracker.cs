using Atlas.Application.AiPlatform.Models;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class MultiAgentExecutionTracker
{
    private readonly ConcurrentDictionary<long, Channel<MultiAgentStreamEvent>> _channels = new();

    public ChannelReader<MultiAgentStreamEvent> Create(long executionId)
    {
        var channel = Channel.CreateUnbounded<MultiAgentStreamEvent>();
        _channels[executionId] = channel;
        return channel.Reader;
    }

    public ValueTask PublishAsync(long executionId, MultiAgentStreamEvent evt, CancellationToken cancellationToken)
    {
        if (_channels.TryGetValue(executionId, out var channel))
        {
            return channel.Writer.WriteAsync(evt, cancellationToken);
        }

        return ValueTask.CompletedTask;
    }

    public void Complete(long executionId, Exception? ex = null)
    {
        if (_channels.TryRemove(executionId, out var channel))
        {
            if (ex is null)
            {
                channel.Writer.TryComplete();
            }
            else
            {
                channel.Writer.TryComplete(ex);
            }
        }
    }
}
