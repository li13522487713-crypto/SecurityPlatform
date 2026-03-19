using System.Text.Json;
using Atlas.Domain.AiPlatform.Enums;

namespace Atlas.Infrastructure.Services.WorkflowEngine.NodeExecutors;

/// <summary>
/// 循环节点：支持 count / while / forEach 三种模式。
/// Config 参数：
/// - mode: count | while | forEach（默认 count）
/// - maxIterations: 最大迭代次数（默认 10）
/// - indexVariable: 索引变量名（默认 loop_index）
/// - condition: while 条件表达式（mode=while 必填）
/// - collectionPath: 集合变量路径（mode=forEach 必填）
/// - itemVariable: 当前项变量名（mode=forEach，默认 loop_item）
/// - itemIndexVariable: 当前项索引变量名（mode=forEach，默认 loop_item_index）
/// 输出变量：
/// - loop_completed: 是否完成
/// - loop_break_reason: 结束原因（可选）
/// </summary>
public sealed class LoopNodeExecutor : INodeExecutor
{
    public WorkflowNodeType NodeType => WorkflowNodeType.Loop;

    public Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        var outputs = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        var mode = context.GetConfigString("mode", "count").Trim().ToLowerInvariant();
        var indexVariable = context.GetConfigString("indexVariable", "loop_index");
        var currentIndex = ResolveCurrentIndex(context, indexVariable);
        var maxIterations = NormalizeMaxIterations(context.GetConfigInt32("maxIterations", 10));

        if (currentIndex >= maxIterations)
        {
            outputs[indexVariable] = JsonSerializer.SerializeToElement(currentIndex);
            outputs["loop_completed"] = JsonSerializer.SerializeToElement(true);
            outputs["loop_break_reason"] = VariableResolver.CreateStringElement("max_iterations_reached");
            return Task.FromResult(new NodeExecutionResult(true, outputs));
        }

        return mode switch
        {
            "while" => Task.FromResult(RunWhile(context, outputs, indexVariable, currentIndex)),
            "foreach" => Task.FromResult(RunForEach(context, outputs, indexVariable, currentIndex)),
            _ => Task.FromResult(RunCount(outputs, indexVariable, currentIndex, maxIterations))
        };
    }

    private static NodeExecutionResult RunCount(
        Dictionary<string, JsonElement> outputs,
        string indexVariable,
        int currentIndex,
        int maxIterations)
    {
        if (currentIndex < maxIterations)
        {
            outputs[indexVariable] = JsonSerializer.SerializeToElement(currentIndex + 1);
            outputs["loop_completed"] = JsonSerializer.SerializeToElement(false);
        }
        else
        {
            outputs[indexVariable] = JsonSerializer.SerializeToElement(currentIndex);
            outputs["loop_completed"] = JsonSerializer.SerializeToElement(true);
        }

        return new NodeExecutionResult(true, outputs);
    }

    private static NodeExecutionResult RunWhile(
        NodeExecutionContext context,
        Dictionary<string, JsonElement> outputs,
        string indexVariable,
        int currentIndex)
    {
        var condition = context.GetConfigString("condition");
        if (string.IsNullOrWhiteSpace(condition))
        {
            return new NodeExecutionResult(false, outputs, "Loop 节点 mode=while 时 condition 不能为空。");
        }

        var shouldContinue = context.EvaluateCondition(condition);
        outputs[indexVariable] = JsonSerializer.SerializeToElement(shouldContinue ? currentIndex + 1 : currentIndex);
        outputs["loop_completed"] = JsonSerializer.SerializeToElement(!shouldContinue);
        return new NodeExecutionResult(true, outputs);
    }

    private static NodeExecutionResult RunForEach(
        NodeExecutionContext context,
        Dictionary<string, JsonElement> outputs,
        string indexVariable,
        int currentIndex)
    {
        var collectionPath = context.GetConfigString("collectionPath");
        if (string.IsNullOrWhiteSpace(collectionPath))
        {
            return new NodeExecutionResult(false, outputs, "Loop 节点 mode=forEach 时 collectionPath 不能为空。");
        }

        if (!context.TryResolveVariable(collectionPath, out var collection))
        {
            return new NodeExecutionResult(false, outputs, $"Loop 节点未找到集合变量：{collectionPath}");
        }

        if (collection.ValueKind != JsonValueKind.Array)
        {
            return new NodeExecutionResult(false, outputs, $"Loop 节点集合变量 {collectionPath} 不是数组。");
        }

        var itemVariable = context.GetConfigString("itemVariable", "loop_item");
        var itemIndexVariable = context.GetConfigString("itemIndexVariable", "loop_item_index");
        var total = collection.GetArrayLength();
        if (currentIndex >= total)
        {
            outputs[indexVariable] = JsonSerializer.SerializeToElement(currentIndex);
            outputs["loop_completed"] = JsonSerializer.SerializeToElement(true);
            outputs["loop_break_reason"] = VariableResolver.CreateStringElement("source_exhausted");
            return new NodeExecutionResult(true, outputs);
        }

        outputs[indexVariable] = JsonSerializer.SerializeToElement(currentIndex + 1);
        outputs[itemIndexVariable] = JsonSerializer.SerializeToElement(currentIndex);
        outputs[itemVariable] = collection[currentIndex].Clone();
        outputs["loop_completed"] = JsonSerializer.SerializeToElement(false);
        return new NodeExecutionResult(true, outputs);
    }

    private static int ResolveCurrentIndex(NodeExecutionContext context, string indexVariable)
    {
        if (!context.Variables.TryGetValue(indexVariable, out var indexValue))
        {
            return 0;
        }

        if (indexValue.ValueKind == JsonValueKind.Number && indexValue.TryGetInt32(out var numericIndex))
        {
            return Math.Max(0, numericIndex);
        }

        var text = VariableResolver.ToDisplayText(indexValue);
        return int.TryParse(text, out var parsedIndex)
            ? Math.Max(0, parsedIndex)
            : 0;
    }

    private static int NormalizeMaxIterations(int maxIterations)
    {
        if (maxIterations <= 0)
        {
            return 10;
        }

        return Math.Min(maxIterations, 10_000);
    }
}
