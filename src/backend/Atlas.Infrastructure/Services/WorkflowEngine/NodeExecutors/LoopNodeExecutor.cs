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

        string loopType = "count"; 
        if (context.Node.Config.TryGetValue("inputs", out var inputsRaw) && inputsRaw.ValueKind == JsonValueKind.Object)
        {
            if (inputsRaw.TryGetProperty("loopType", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
            {
                loopType = typeProp.GetString()?.ToLowerInvariant() ?? "count";
            }
        }
        else if (context.Node.Config.TryGetValue("mode", out var modeProp) && modeProp.ValueKind == JsonValueKind.String)
        {
            loopType = modeProp.GetString()?.ToLowerInvariant() ?? "count";
        }

        string nodeKey = context.Node.Key;
        string indexKey = $"{nodeKey}.locals.index";
        int currentIndex = ResolveCurrentIndex(context, indexKey);

        if (loopType == "array")
        {
            return Task.FromResult(RunArrayLoop(context, outputs, nodeKey, indexKey, currentIndex, inputsRaw));
        }
        else if (loopType == "infinite" || loopType == "while")
        {
            return Task.FromResult(RunInfiniteLoop(outputs, indexKey, currentIndex));
        }
        else 
        {
            return Task.FromResult(RunCountLoop(context, outputs, indexKey, currentIndex, inputsRaw));
        }
    }

    private static NodeExecutionResult RunCountLoop(
        NodeExecutionContext context,
        Dictionary<string, JsonElement> outputs,
        string indexKey,
        int currentIndex,
        JsonElement inputsRaw)
    {
        int maxIterations = 10;
        if (inputsRaw.ValueKind == JsonValueKind.Object && inputsRaw.TryGetProperty("loopCount", out var loopCountExpr))
        {
            maxIterations = ExtractValueExpressionInt32(loopCountExpr) ?? 10;
        }
        else
        {
            maxIterations = context.GetConfigInt32("maxIterations", 10);
        }
        
        maxIterations = Math.Max(1, Math.Min(maxIterations, 10_000));

        if (currentIndex < maxIterations)
        {
            outputs[indexKey] = JsonSerializer.SerializeToElement(currentIndex + 1);
            outputs["loop_completed"] = JsonSerializer.SerializeToElement(false);
        }
        else
        {
            outputs[indexKey] = JsonSerializer.SerializeToElement(currentIndex);
            outputs["loop_completed"] = JsonSerializer.SerializeToElement(true);
        }

        return new NodeExecutionResult(true, outputs);
    }

    private static NodeExecutionResult RunInfiniteLoop(
        Dictionary<string, JsonElement> outputs,
        string indexKey,
        int currentIndex)
    {
        outputs[indexKey] = JsonSerializer.SerializeToElement(currentIndex + 1);
        outputs["loop_completed"] = JsonSerializer.SerializeToElement(false);
        return new NodeExecutionResult(true, outputs);
    }

    private static NodeExecutionResult RunArrayLoop(
        NodeExecutionContext context,
        Dictionary<string, JsonElement> outputs,
        string nodeKey,
        string indexKey,
        int currentIndex,
        JsonElement inputsRaw)
    {
        string inputParamName = "input";
        if (inputsRaw.ValueKind == JsonValueKind.Object && 
            inputsRaw.TryGetProperty("inputParameters", out var inputParams) && 
            inputParams.ValueKind == JsonValueKind.Array)
        {
            foreach (var ip in inputParams.EnumerateArray())
            {
                if (ip.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                {
                    inputParamName = n.GetString() ?? "input";
                    break;
                }
            }
        }

        string collectionKey = inputParamName;
        
        if (!context.Variables.TryGetValue(collectionKey, out var collection))
        {
            if (!context.TryResolveVariable(context.GetConfigString("collectionPath"), out collection))
            {
                outputs[indexKey] = JsonSerializer.SerializeToElement(currentIndex);
                outputs["loop_completed"] = JsonSerializer.SerializeToElement(true);
                outputs["loop_break_reason"] = VariableResolver.CreateStringElement("source_not_found");
                return new NodeExecutionResult(true, outputs);
            }
        }

        if (collection.ValueKind != JsonValueKind.Array)
        {
            if (collection.ValueKind == JsonValueKind.String)
            {
                try 
                {
                    var parsed = JsonSerializer.Deserialize<JsonElement>(collection.GetString() ?? "[]");
                    if (parsed.ValueKind == JsonValueKind.Array) collection = parsed;
                } 
                catch {}
            }
        }

        if (collection.ValueKind != JsonValueKind.Array)
        {
            outputs[indexKey] = JsonSerializer.SerializeToElement(currentIndex);
            outputs["loop_completed"] = JsonSerializer.SerializeToElement(true);
            outputs["loop_break_reason"] = VariableResolver.CreateStringElement("source_not_array");
            return new NodeExecutionResult(true, outputs);
        }

        int total = collection.GetArrayLength();
        if (currentIndex >= total)
        {
            outputs[indexKey] = JsonSerializer.SerializeToElement(currentIndex);
            outputs["loop_completed"] = JsonSerializer.SerializeToElement(true);
            outputs["loop_break_reason"] = VariableResolver.CreateStringElement("source_exhausted");
            return new NodeExecutionResult(true, outputs);
        }

        outputs[indexKey] = JsonSerializer.SerializeToElement(currentIndex + 1);
        outputs[$"{nodeKey}.locals.{inputParamName}"] = collection[currentIndex].Clone();
        outputs["loop_completed"] = JsonSerializer.SerializeToElement(false);
        return new NodeExecutionResult(true, outputs);
    }

    private static int ResolveCurrentIndex(NodeExecutionContext context, string indexKey)
    {
        if (context.Variables.TryGetValue(indexKey, out var indexValue))
        {
            if (indexValue.ValueKind == JsonValueKind.Number && indexValue.TryGetInt32(out var numericIndex))
            {
                return Math.Max(0, numericIndex);
            }

            var text = VariableResolver.ToDisplayText(indexValue);
            if (int.TryParse(text, out var parsedIndex))
            {
                return Math.Max(0, parsedIndex);
            }
        }
        
        // Legacy fallback
        if (context.Variables.TryGetValue("loop_index", out var legacyIndexValue))
        {
            if (legacyIndexValue.ValueKind == JsonValueKind.Number && legacyIndexValue.TryGetInt32(out var numericIndex))
                return Math.Max(0, numericIndex);
        }

        return 0;
    }

    private static int? ExtractValueExpressionInt32(JsonElement expr)
    {
        if (expr.ValueKind != JsonValueKind.Object) return null;
        if (!expr.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String) return null;
        if (typeProp.GetString() != "literal") return null;
        if (!expr.TryGetProperty("content", out var contentProp)) return null;
        if (contentProp.ValueKind == JsonValueKind.Number && contentProp.TryGetInt32(out var i)) return i;
        if (contentProp.ValueKind == JsonValueKind.String && int.TryParse(contentProp.GetString(), out var i2)) return i2;
        return null;
    }
}
