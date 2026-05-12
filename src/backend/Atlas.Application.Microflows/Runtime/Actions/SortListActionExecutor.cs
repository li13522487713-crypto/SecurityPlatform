using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Actions;

/// <summary>
/// 列表排序动作：按指定字段升降序排序输入列表，输出新列表。
/// </summary>
public sealed class SortListActionExecutor : ListActionExecutorBase
{
    public override string ActionKind => "sortList";

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
            ?? "sorted";
        var direction = (ReadString(context.ActionConfig, "direction") ?? "asc").ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(sourceListVariableName))
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeVariableNotFound, "Sort List requires sourceListVariableName/listVariableName."));
        }

        var listElement = ReadListFromVariable(context, sourceListVariableName);
        if (listElement.ValueKind != JsonValueKind.Array)
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeVariableTypeMismatch, $"Source variable '{sourceListVariableName}' is not a list."));
        }

        var items = listElement.EnumerateArray().Select(item => item.Clone()).ToList();
        var itemVariableName = NormalizeVariableName(ReadFirstString(context.ActionConfig, "objectVariableName", "itemVariableName", "itemVariable"), "item");
        var sortKeys = ReadSortKeys(context.ActionConfig);
        if (sortKeys.Count == 0)
        {
            var sortExpression = ReadExpressionRaw(ReadProperty(context.ActionConfig, "sortExpression"));
            if (!string.IsNullOrWhiteSpace(sortExpression))
            {
                sortKeys.Add(new SortKeySpec(null, direction, sortExpression));
            }
            else
            {
                var sortField = ReadString(context.ActionConfig, "sortField")
                    ?? ReadString(context.ActionConfig, "memberName");
                if (!string.IsNullOrWhiteSpace(sortField))
                {
                    sortKeys.Add(new SortKeySpec(sortField, direction, null));
                }
            }
        }
        items.Sort((left, right) =>
        {
            foreach (var sortKey in sortKeys)
            {
                var leftValue = ResolveSortValue(context, left, itemVariableName, sortKey.ExpressionRaw, sortKey.Field);
                var rightValue = ResolveSortValue(context, right, itemVariableName, sortKey.ExpressionRaw, sortKey.Field);
                var comparison = CompareJsonElements(leftValue, rightValue);
                if (comparison != 0)
                {
                    return string.Equals(sortKey.Direction, "desc", StringComparison.OrdinalIgnoreCase) ? -comparison : comparison;
                }
            }

            return 0;
        });

        var output = JsonSerializer.SerializeToElement(items, JsonOptions);
        SetListVariable(context, outputName, output, MicroflowVariableSourceKind.ActionOutput);
        started.Stop();

        var producedDataType = JsonSerializer.SerializeToElement(new { kind = "list" }, JsonOptions);
        var rawValueJson = output.GetRawText();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = output,
            OutputPreview = $"{outputName}[{items.Count}]",
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

    private static JsonElement ResolveSortValue(
        MicroflowActionExecutionContext context,
        JsonElement item,
        string itemVariableName,
        string? sortExpression,
        string? sortField)
    {
        if (string.IsNullOrWhiteSpace(sortExpression))
        {
            return ExtractSortValue(item, sortField);
        }

        using var scopeLease = context.RuntimeExecutionContext.PushLoopScope(
            loopObjectId: context.ObjectId,
            collectionId: context.CollectionId ?? context.ObjectId,
            iteratorVariableName: itemVariableName,
            index: 0,
            iteratorRawValue: item,
            iteratorPreview: MicroflowVariableStore.Preview(item.GetRawText()),
            iteratorDataTypeJson: InferDataType(item).GetRawText(),
            defineIterator: true);
        return TryEvaluateExpression(context, sortExpression, out var evaluated, out _)
            ? evaluated
            : JsonNull();
    }

    private static JsonElement ExtractSortValue(JsonElement element, string? sortField)
    {
        if (string.IsNullOrWhiteSpace(sortField))
        {
            return element;
        }

        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(sortField, out var value))
        {
            return value;
        }

        return default;
    }

    private static int CompareJsonElements(JsonElement left, JsonElement right)
    {
        if (left.ValueKind == JsonValueKind.Undefined || left.ValueKind == JsonValueKind.Null)
        {
            return right.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? 0 : -1;
        }

        if (right.ValueKind == JsonValueKind.Undefined || right.ValueKind == JsonValueKind.Null)
        {
            return 1;
        }

        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.Number)
        {
            return left.GetDecimal().CompareTo(right.GetDecimal());
        }

        if (left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.String)
        {
            return string.Compare(left.GetString(), right.GetString(), StringComparison.Ordinal);
        }

        if (left.ValueKind is JsonValueKind.True or JsonValueKind.False
            && right.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return left.GetBoolean().CompareTo(right.GetBoolean());
        }

        // Fallback: compare raw text representation deterministically.
        return string.Compare(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
    }

    private static List<SortKeySpec> ReadSortKeys(JsonElement config)
    {
        var keys = new List<SortKeySpec>();
        if (config.ValueKind == JsonValueKind.Object
            && config.TryGetProperty("sortKeys", out var sortKeys)
            && sortKeys.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in sortKeys.EnumerateArray())
            {
                var field = ReadFirstString(item, "field", "memberName", "attributeQualifiedName", "sortField");
                var expressionRaw = ReadExpressionRaw(ReadProperty(item, "expression"));
                if (string.IsNullOrWhiteSpace(field) && string.IsNullOrWhiteSpace(expressionRaw))
                {
                    continue;
                }

                var direction = ReadFirstString(item, "direction") ?? "asc";
                keys.Add(new SortKeySpec(field, direction, expressionRaw));
            }
        }

        return keys;
    }

    private sealed record SortKeySpec(string? Field, string Direction, string? ExpressionRaw);
}
