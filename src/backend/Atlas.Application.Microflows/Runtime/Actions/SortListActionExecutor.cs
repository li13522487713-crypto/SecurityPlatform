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
        var sortField = ReadString(context.ActionConfig, "sortField")
            ?? ReadString(context.ActionConfig, "memberName");
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
        var ascending = !string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        items.Sort((left, right) =>
        {
            var leftValue = ExtractSortValue(left, sortField);
            var rightValue = ExtractSortValue(right, sortField);
            var comparison = CompareJsonElements(leftValue, rightValue);
            return ascending ? comparison : -comparison;
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
}
