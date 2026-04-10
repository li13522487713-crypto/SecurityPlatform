using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// Break 节点：向循环控制流输出中断信号。
/// </summary>
public sealed class BreakNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Break;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["loop_break"] = JsonSerializer.SerializeToElement(true)
        };

        var reason = context.GetConfigString("reason");
        if (!string.IsNullOrWhiteSpace(reason))
        {
            outputs["loop_break_reason"] = VariableResolver.CreateStringElement(context.ReplaceVariables(reason));
        }

        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}
