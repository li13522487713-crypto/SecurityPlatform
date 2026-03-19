using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// JSON 反序列化节点：将 JSON 字符串反序列化为变量。
/// Config 参数：inputVariable（变量名，其值为 JSON 字符串）
/// </summary>
public sealed class JsonDeserializationNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.JsonDeserialization;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var inputVariable = context.GetConfigString("inputVariable");
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!context.TryResolveVariable(inputVariable, out var source))
            {
                return Task.FromResult(new NodeExecutionResult(true, outputs));
            }

            var objectPayload = ParseToObject(source);
            foreach (var kvp in objectPayload)
            {
                outputs[kvp.Key] = kvp.Value;
            }

            return Task.FromResult(new NodeExecutionResult(true, outputs));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new NodeExecutionResult(false, outputs, $"JSON 反序列化失败: {ex.Message}"));
        }
    }

    private static Dictionary<string, JsonElement> ParseToObject(JsonElement source)
    {
        if (source.ValueKind == JsonValueKind.Object)
        {
            var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in source.EnumerateObject())
            {
                values[property.Name] = property.Value.Clone();
            }

            return values;
        }

        if (source.ValueKind == JsonValueKind.String)
        {
            var json = source.GetString();
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            }

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            }

            var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = property.Value.Clone();
            }

            return values;
        }

        return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
    }
}
