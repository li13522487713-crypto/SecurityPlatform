using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>结束节点：按 Coze inputParameters/terminatePlan 返回指定输出。</summary>
public sealed class ExitNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Exit;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        CollectMappedOutputs(context, outputs);
        CollectCozeInputParameterOutputs(context, outputs);

        if (outputs.Count == 0)
        {
            foreach (var variable in context.Variables)
            {
                outputs[variable.Key] = variable.Value.Clone();
            }
        }

        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }

    private static void CollectMappedOutputs(
        NodeExecutionContext context,
        IDictionary<string, JsonElement> outputs)
    {
        if (!context.Node.Config.TryGetValue("inputMappings", out var mappings) ||
            mappings.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var mapping in mappings.EnumerateObject())
        {
            var field = mapping.Name.Trim();
            if (field.Length == 0 || outputs.ContainsKey(field))
            {
                continue;
            }

            if (context.Variables.TryGetValue(field, out var preparedValue))
            {
                outputs[field] = preparedValue.Clone();
                continue;
            }

            if (mapping.Value.ValueKind == JsonValueKind.String &&
                TryResolveOutputValue(context, mapping.Value.GetString(), out var mappedValue))
            {
                outputs[field] = mappedValue.Clone();
            }
        }
    }

    private static void CollectCozeInputParameterOutputs(
        NodeExecutionContext context,
        IDictionary<string, JsonElement> outputs)
    {
        if (!context.Node.Config.TryGetValue("inputs", out var inputs) ||
            inputs.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(inputs, "inputParameters", out var inputParameters) ||
            inputParameters.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var parameter in inputParameters.EnumerateArray())
        {
            if (parameter.ValueKind != JsonValueKind.Object ||
                !TryGetString(parameter, "name", out var field) ||
                outputs.ContainsKey(field))
            {
                continue;
            }

            if (context.Variables.TryGetValue(field, out var preparedValue))
            {
                outputs[field] = preparedValue.Clone();
                continue;
            }

            if (TryResolveCozeInputValue(context, parameter, out var resolvedValue))
            {
                outputs[field] = resolvedValue.Clone();
            }
        }
    }

    private static bool TryResolveCozeInputValue(
        NodeExecutionContext context,
        JsonElement parameter,
        out JsonElement value)
    {
        value = default;
        if (!TryGetProperty(parameter, "input", out var input) ||
            input.ValueKind != JsonValueKind.Object ||
            !TryGetProperty(input, "value", out var valueElement) ||
            valueElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var valueType = TryGetString(valueElement, "type", out var type) ? type : string.Empty;
        if (!TryGetProperty(valueElement, "content", out var content))
        {
            return false;
        }

        if (string.Equals(valueType, "ref", StringComparison.OrdinalIgnoreCase) &&
            content.ValueKind == JsonValueKind.Object &&
            TryGetString(content, "blockID", out var blockId) &&
            TryGetString(content, "name", out var name))
        {
            return TryResolveOutputValue(context, $"{blockId}.{name}", out value);
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            value = context.ParseLiteralOrTemplate(content.GetString() ?? string.Empty);
            return true;
        }

        if (content.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        value = content.Clone();
        return true;
    }

    private static bool TryResolveOutputValue(
        NodeExecutionContext context,
        string? path,
        out JsonElement value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Trim();
        if (context.TryResolveVariable(normalized, out value))
        {
            return true;
        }

        var dotIndex = normalized.IndexOf('.');
        if (dotIndex > 0 && dotIndex < normalized.Length - 1)
        {
            var field = normalized[(dotIndex + 1)..];
            if (context.Variables.TryGetValue(field, out value))
            {
                return true;
            }
        }

        return context.Variables.TryGetValue(normalized, out value);
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
