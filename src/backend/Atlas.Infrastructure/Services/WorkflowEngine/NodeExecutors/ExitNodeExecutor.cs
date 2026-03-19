using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>结束节点：收集所有变量作为输出。</summary>
public sealed class ExitNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Exit;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        // 将当前所有变量作为输出
        var outputs = new Dictionary<string, JsonElement>(context.Variables, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}
