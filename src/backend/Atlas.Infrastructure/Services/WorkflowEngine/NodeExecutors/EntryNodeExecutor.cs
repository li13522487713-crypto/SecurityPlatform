using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>开始节点：按 Coze data.outputs 暴露试运行输入为 block output。</summary>
public sealed class EntryNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Entry;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (context.Node.Config.TryGetValue("entryOutputs", out var entryOutputs) &&
            entryOutputs.ValueKind == JsonValueKind.Array)
        {
            foreach (var output in entryOutputs.EnumerateArray())
            {
                if (output.ValueKind != JsonValueKind.Object ||
                    !TryGetString(output, "name", out var name))
                {
                    continue;
                }

                if (context.Variables.TryGetValue(name, out var inputValue))
                {
                    outputs[name] = inputValue.Clone();
                    continue;
                }

                if (TryGetProperty(output, "defaultValue", out var defaultValue) &&
                    defaultValue.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
                {
                    outputs[name] = defaultValue.Clone();
                }
            }
        }

        if (outputs.Count == 0)
        {
            foreach (var variable in context.Variables)
            {
                outputs[variable.Key] = variable.Value.Clone();
            }
        }

        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!TryGetProperty(element, propertyName, out var raw) || raw.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = raw.GetString()?.Trim() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
