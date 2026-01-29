using System.Threading.Channels;
using Atlas.WorkflowCore.Abstractions;
using Atlas.WorkflowCore.Models;

namespace Atlas.WorkflowCore.Services.DefaultProviders;

/// <summary>
/// 单节点队列提供者 - 使用内存 Channel 实现
/// </summary>
public class SingleNodeQueueProvider : IQueueProvider
{
    private readonly Dictionary<QueueType, Channel<string>> _queues;
    private bool _isRunning;

    public SingleNodeQueueProvider()
    {
        _queues = new Dictionary<QueueType, Channel<string>>
        {
            [QueueType.Workflow] = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            }),
            [QueueType.Event] = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            }),
            [QueueType.Index] = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            })
        };
    }

    public bool IsDequeueBlocking => true;

    public async Task QueueWork(string id, QueueType queue)
    {
        if (!_isRunning)
        {
            throw new InvalidOperationException("队列提供者未启动");
        }

        if (!_queues.TryGetValue(queue, out var channel))
        {
            throw new ArgumentException($"不支持的队列类型: {queue}", nameof(queue));
        }

        await channel.Writer.WriteAsync(id);
    }

    public async Task<string?> DequeueWork(QueueType queue, CancellationToken cancellationToken)
    {
        if (!_isRunning)
        {
            return null;
        }

        if (!_queues.TryGetValue(queue, out var channel))
        {
            throw new ArgumentException($"不支持的队列类型: {queue}", nameof(queue));
        }

        try
        {
            return await channel.Reader.ReadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public Task Start()
    {
        _isRunning = true;
        return Task.CompletedTask;
    }

    public Task Stop()
    {
        _isRunning = false;

        // 完成所有写入器
        foreach (var channel in _queues.Values)
        {
            channel.Writer.Complete();
        }

        return Task.CompletedTask;
    }
}
