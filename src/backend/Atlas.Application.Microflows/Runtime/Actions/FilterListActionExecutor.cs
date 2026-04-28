using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Expressions;

namespace Atlas.Application.Microflows.Runtime.Actions;

/// <summary>
/// 列表过滤动作：遍历输入列表，对每个 item 求条件表达式并保留 true 的元素。
/// </summary>
public sealed class FilterListActionExecutor : ListActionExecutorBase
{
    public override string ActionKind => "filterList";

    public override Task<MicroflowActionExecutionResult> ExecuteAsync(
        MicroflowActionExecutionContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        var sourceListVariableName = ReadString(context.ActionConfig, "listVariableName")
            ?? ReadString(context.ActionConfig, "sourceListVariableName");
        var outputName = ReadString(context.ActionConfig, "outputVariableName")
            ?? ReadString(context.ActionConfig, "resultVariableName")
            ?? "filtered";
        var itemVariableName = ReadString(context.ActionConfig, "itemVariableName") ?? "$item";
        var conditionExpression = ReadString(context.ActionConfig, "conditionExpression")
            ?? ReadString(context.ActionConfig, "filterExpression");

        if (string.IsNullOrWhiteSpace(sourceListVariableName))
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeVariableNotFound, "Filter List requires sourceListVariableName/listVariableName."));
        }

        var listElement = ReadListFromVariable(context, sourceListVariableName);
        if (listElement.ValueKind != JsonValueKind.Array)
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeVariableTypeMismatch, $"Source variable '{sourceListVariableName}' is not a list."));
        }

        var filtered = new List<JsonElement>();
        if (string.IsNullOrWhiteSpace(conditionExpression) || context.ExpressionEvaluator is null)
        {
            // Without an expression there is no way to filter; return the source list unchanged
            // and record a warning so callers know the action degenerated to a copy.
            foreach (var item in listElement.EnumerateArray())
            {
                filtered.Add(item.Clone());
            }
        }
        else
        {
            foreach (var item in listElement.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                using var scopeLease = context.RuntimeExecutionContext.PushLoopScope(
                    loopObjectId: context.ObjectId,
                    collectionId: context.CollectionId ?? context.ObjectId,
                    iteratorVariableName: itemVariableName,
                    index: filtered.Count,
                    iteratorRawValue: item,
                    iteratorPreview: MicroflowVariableStore.Preview(item.GetRawText()),
                    defineIterator: true);
                var evaluation = context.ExpressionEvaluator.Evaluate(
                    conditionExpression!,
                    new MicroflowExpressionEvaluationContext
                    {
                        RuntimeExecutionContext = context.RuntimeExecutionContext,
                        VariableStore = context.VariableStore,
                        MetadataCatalog = context.MetadataCatalog,
                        CurrentObjectId = context.ObjectId,
                        CurrentActionId = context.ActionId,
                        Mode = MicroflowRuntimeExecutionMode.TestRun
                    });
                if (!evaluation.Success || string.IsNullOrEmpty(evaluation.RawValueJson))
                {
                    return Task.FromResult(Failed(started, evaluation.Error?.Code ?? RuntimeErrorCode.RuntimeExpressionError, evaluation.Error?.Message ?? "Filter expression failed."));
                }

                var resultElement = MicroflowVariableStore.ToJsonElement(evaluation.RawValueJson) ?? default;
                if (resultElement.ValueKind == JsonValueKind.True)
                {
                    filtered.Add(item.Clone());
                }
            }
        }

        var output = JsonSerializer.SerializeToElement(filtered, JsonOptions);
        SetListVariable(context, outputName, output, MicroflowVariableSourceKind.ActionOutput);
        started.Stop();

        var producedDataType = JsonSerializer.SerializeToElement(new { kind = "list" }, JsonOptions);
        var rawValueJson = output.GetRawText();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = output,
            OutputPreview = $"{outputName}[{filtered.Count}]",
            ProducedVariables =
            [
                new MicroflowRuntimeVariableValueDto
                {
                    Name = outputName,
                    Type = producedDataType,
                    RawValue = output,
                    RawValueJson = rawValueJson,
                    ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
                    Source = MicroflowVariableSourceKind.ActionOutput,
                    ScopeKind = "local"
                }
            ],
            DurationMs = (int)started.ElapsedMilliseconds
        });
    }
}
