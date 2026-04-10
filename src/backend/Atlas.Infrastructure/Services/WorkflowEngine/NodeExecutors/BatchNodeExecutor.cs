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

        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["batch_concurrent_size"] = JsonSerializer.SerializeToElement(concurrentSize),
            ["batch_size"] = JsonSerializer.SerializeToElement(batchSize),
            ["batch_input_array_path"] = VariableResolver.CreateStringElement(inputArrayPath),
            ["batch_item_variable"] = VariableResolver.CreateStringElement(itemVariable),
            ["batch_item_index_variable"] = VariableResolver.CreateStringElement(itemIndexVariable),
            ["batch_output_key"] = VariableResolver.CreateStringElement(outputKey)
        };

        return Task.FromResult(new NodeExecutionResult(true, outputs));
    }
}
