using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// Batch 节点：由 DagExecutor 驱动子画布并发执行，本执行器仅输出批处理配置归一化结果。
/// </summary>
public sealed class BatchNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Batch;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var concurrentSize = Math.Clamp(context.GetConfigInt32("concurrentSize", 4), 1, 64);
        var batchSize = Math.Clamp(context.GetConfigInt32("batchSize", 1), 1, 10_000);
        var inputArrayPath = context.GetConfigString("inputArrayPath");
        var itemVariable = context.GetConfigString("itemVariable", "batch_item");
        var itemIndexVariable = context.GetConfigString("itemIndexVariable", "batch_item_index");
        var outputKey = context.GetConfigString("outputKey", "batch_results");
        var operation = context.GetConfigString("mapOperation", context.GetConfigString("operation", "identity"))
            .Trim()
            .ToLowerInvariant();

        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["batch_concurrent_size"] = JsonSerializer.SerializeToElement(concurrentSize),
            ["batch_size"] = JsonSerializer.SerializeToElement(batchSize),
            ["batch_input_array_path"] = VariableResolver.CreateStringElement(inputArrayPath),
            ["batch_item_variable"] = VariableResolver.CreateStringElement(itemVariable),
            ["batch_item_index_variable"] = VariableResolver.CreateStringElement(itemIndexVariable),
            ["batch_output_key"] = VariableResolver.CreateStringElement(outputKey)
        };

        if (!string.IsNullOrWhiteSpace(inputArrayPath) &&
            context.TryResolveVariable(inputArrayPath, out var inputArray) &&
            TryNormalizeArray(inputArray, out var array))
        {
            var results = new List<JsonElement>();
            var index = 0;
            foreach (var item in array.EnumerateArray())
            {
                outputs[itemVariable] = item.Clone();
                outputs[itemIndexVariable] = JsonSerializer.SerializeToElement(index);
                results.Add(ApplyProjection(item, operation));
                index++;
            }

            outputs[outputKey] = JsonSerializer.SerializeToElement(results);
            outputs["batch_completed"] = JsonSerializer.SerializeToElement(true);
            outputs["batch_error_count"] = JsonSerializer.SerializeToElement(0);
        }

        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }

    private static bool TryNormalizeArray(JsonElement raw, out JsonElement array)
    {
        array = raw;
        if (raw.ValueKind == JsonValueKind.Array)
        {
            return true;
        }

        if (raw.ValueKind == JsonValueKind.String)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<JsonElement>(raw.GetString() ?? "[]");
                if (parsed.ValueKind == JsonValueKind.Array)
                {
                    array = parsed;
                    return true;
                }
            }
            catch
            {
                // Ignore malformed array strings and report no projection output.
            }
        }

        return false;
    }

    private static JsonElement ApplyProjection(JsonElement item, string operation)
    {
        if (operation is "upper" or "uppercase")
        {
            return VariableResolver.CreateStringElement(VariableResolver.ToDisplayText(item).ToUpperInvariant());
        }

        if (operation is "double" or "multiply2" or "times2" &&
            item.ValueKind == JsonValueKind.Number &&
            item.TryGetDecimal(out var numeric))
        {
            return JsonSerializer.SerializeToElement(numeric * 2);
        }

        return item.Clone();
    }
}
