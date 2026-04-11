using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 循环内变量赋值节点：与 AssignVariable 语义一致，但用于 Loop 场景。
/// Config 参数：assignments（格式 "key1=value1;key2=value2"）
/// </summary>
public sealed class VariableAssignerWithinLoopNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.VariableAssignerWithinLoop;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var assignments = context.GetConfigString("assignments");

        foreach (var pair in assignments.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            var value = context.ParseLiteralOrTemplate(parts[1]);
            outputs[parts[0]] = value;
        }

        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}

