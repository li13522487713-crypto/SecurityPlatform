using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>开始节点：直接透传输入变量。</summary>
public sealed class EntryNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Entry;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(new NodeExecutionResult(true, new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)));
    }
}
