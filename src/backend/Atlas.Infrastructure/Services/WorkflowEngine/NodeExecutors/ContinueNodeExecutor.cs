using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// Continue 节点：向循环控制流输出继续信号。
/// </summary>
public sealed class ContinueNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Continue;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["loop_continue"] = JsonSerializer.SerializeToElement(true)
        };
        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}
