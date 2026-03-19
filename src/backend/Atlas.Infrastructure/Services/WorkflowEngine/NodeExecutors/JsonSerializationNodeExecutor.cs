using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// JSON 序列化节点：将指定变量序列化为 JSON 字符串。
/// Config 参数：variableKeys（逗号分隔）、outputKey（默认 "json_output"）
/// </summary>
public sealed class JsonSerializationNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.JsonSerialization;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var keys = context.GetConfigString("variableKeys")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var outputKey = context.GetConfigString("outputKey", "json_output");

        var data = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            if (context.Variables.TryGetValue(key, out var value))
            {
                data[key] = value;
            }
        }

        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            [outputKey] = VariableResolver.CreateStringElement(JsonSerializer.Serialize(data))
        };

        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}
