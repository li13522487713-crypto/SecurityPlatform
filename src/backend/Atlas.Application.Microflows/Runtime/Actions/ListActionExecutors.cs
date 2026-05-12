using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Expressions;

namespace Atlas.Application.Microflows.Runtime.Actions;

public abstract class ListActionExecutorBase : IMicroflowActionExecutor
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public abstract string ActionKind { get; }

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public abstract Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct);

    protected static void SetListVariable(MicroflowActionExecutionContext context, string name, JsonElement listValue, string sourceKind)
    {
        var dataTypeJson = JsonSerializer.Serialize(new { kind = "list" }, JsonOptions);
        var rawValueJson = listValue.GetRawText();
        var variable = new MicroflowRuntimeVariableValue
        {
            Name = name,
            DataTypeJson = dataTypeJson,
            RawValueJson = rawValueJson,
            ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
            SourceKind = sourceKind,
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId,
            ScopeKind = MicroflowVariableScopeKind.Action
        };

        if (context.VariableStore.Exists(name))
        {
            context.VariableStore.Set(name, variable);
            return;
        }

        context.VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = name,
            DataTypeJson = dataTypeJson,
            RawValueJson = rawValueJson,
            ValuePreview = variable.ValuePreview,
            SourceKind = sourceKind,
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId,
            ScopeKind = MicroflowVariableScopeKind.Action,
            Value = variable
        });
    }

    protected static JsonElement ReadListFromVariable(MicroflowActionExecutionContext context, string? variableName)
    {
        if (!string.IsNullOrWhiteSpace(variableName) && context.VariableStore.TryGet(variableName!, out var variable) && variable?.RawValueJson is not null)
        {
            return MicroflowVariableStore.ToJsonElement(variable.RawValueJson) ?? JsonSerializer.SerializeToElement(Array.Empty<object>(), JsonOptions);
        }

        return JsonSerializer.SerializeToElement(Array.Empty<object>(), JsonOptions);
    }

    protected static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    protected static string? ReadFirstString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = ReadString(element, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    protected static JsonElement? ReadProperty(JsonElement element, params string[] propertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var value))
            {
                return value.Clone();
            }
        }

        return null;
    }

    protected static string? ReadExpressionRaw(JsonElement? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var element = value.Value;
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("raw", out var raw)
            && raw.ValueKind == JsonValueKind.String)
        {
            return raw.GetString();
        }

        return null;
    }

    protected static string NormalizeVariableName(string? raw, string fallback = "")
    {
        var value = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(value))
        {
            return fallback;
        }

        return value.StartsWith("$", StringComparison.Ordinal) ? value[1..] : value;
    }

    protected static bool TryReadInt(JsonElement value, out int result)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result))
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        result = default;
        return false;
    }

    protected static IReadOnlyList<JsonElement> ReadListItemsFromVariable(MicroflowActionExecutionContext context, string? variableName)
    {
        var list = ReadListFromVariable(context, variableName);
        return list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(item => item.Clone()).ToArray()
            : Array.Empty<JsonElement>();
    }

    protected static void SetVariable(
        MicroflowActionExecutionContext context,
        string name,
        JsonElement value,
        JsonElement dataType,
        string sourceKind,
        bool allowShadowing = true)
    {
        var rawValueJson = value.GetRawText();
        var dataTypeJson = dataType.GetRawText();
        var variable = new MicroflowRuntimeVariableValue
        {
            Name = name,
            DataTypeJson = dataTypeJson,
            RawValueJson = rawValueJson,
            ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
            SourceKind = sourceKind,
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId,
            ScopeKind = MicroflowVariableScopeKind.Action
        };

        if (context.VariableStore.Exists(name))
        {
            context.VariableStore.Set(name, variable);
            return;
        }

        context.VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = name,
            DataTypeJson = dataTypeJson,
            RawValueJson = rawValueJson,
            ValuePreview = variable.ValuePreview,
            SourceKind = sourceKind,
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId,
            ScopeKind = MicroflowVariableScopeKind.Action,
            AllowShadowing = allowShadowing,
            Value = variable
        });
    }

    protected static JsonElement JsonNull()
        => JsonSerializer.SerializeToElement<object?>(null, JsonOptions);

    protected static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    protected static JsonElement InferDataType(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Array => JsonSerializer.SerializeToElement(new { kind = "list" }, JsonOptions),
            JsonValueKind.True or JsonValueKind.False => JsonSerializer.SerializeToElement(new { kind = "boolean" }, JsonOptions),
            JsonValueKind.Number => value.TryGetInt64(out _) && !value.GetRawText().Contains('.', StringComparison.Ordinal)
                ? JsonSerializer.SerializeToElement(new { kind = "integer" }, JsonOptions)
                : JsonSerializer.SerializeToElement(new { kind = "decimal" }, JsonOptions),
            JsonValueKind.String => JsonSerializer.SerializeToElement(new { kind = "string" }, JsonOptions),
            JsonValueKind.Object => JsonSerializer.SerializeToElement(new { kind = "object" }, JsonOptions),
            JsonValueKind.Null => JsonSerializer.SerializeToElement(new { kind = "unknown", reason = "null" }, JsonOptions),
            _ => JsonSerializer.SerializeToElement(new { kind = "unknown" }, JsonOptions),
        };

    protected static MicroflowExpressionEvaluationContext CreateEvaluationContext(MicroflowActionExecutionContext context)
        => new()
        {
            RuntimeExecutionContext = context.RuntimeExecutionContext,
            VariableStore = context.VariableStore,
            MetadataCatalog = context.MetadataCatalog,
            CurrentObjectId = context.ObjectId,
            CurrentActionId = context.ActionId,
            Mode = MicroflowRuntimeExecutionMode.TestRun
        };

    protected static bool TryEvaluateExpression(
        MicroflowActionExecutionContext context,
        string? raw,
        out JsonElement value,
        out MicroflowRuntimeErrorDto? error)
    {
        value = default;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (context.ExpressionEvaluator is null)
        {
            error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeExpressionError,
                Message = "Expression evaluator is unavailable."
            };
            return false;
        }

        var evaluation = context.ExpressionEvaluator.Evaluate(raw, CreateEvaluationContext(context));
        if (!evaluation.Success || string.IsNullOrWhiteSpace(evaluation.RawValueJson))
        {
            error = evaluation.Error is null
                ? new MicroflowRuntimeErrorDto
                {
                    Code = RuntimeErrorCode.RuntimeExpressionError,
                    Message = "Expression evaluation failed."
                }
                : new MicroflowRuntimeErrorDto
                {
                    Code = evaluation.Error.Code,
                    Message = evaluation.Error.Message,
                    Details = evaluation.Error.Details
                };
            return false;
        }

        value = MicroflowVariableStore.ToJsonElement(evaluation.RawValueJson) ?? JsonNull();
        return true;
    }

    protected static MicroflowActionExecutionResult Failed(Stopwatch started, string code, string message)
    {
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Failed,
            Error = new MicroflowRuntimeErrorDto { Code = code, Message = message },
            Message = message,
            ShouldContinueNormalFlow = false,
            ShouldStopRun = true,
            DurationMs = (int)started.ElapsedMilliseconds
        };
    }
}

public sealed class CreateListActionExecutor : ListActionExecutorBase
{
    public override string ActionKind => "createList";

    public override Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        var outputName = ReadFirstString(
                context.ActionConfig,
                "outputListVariableName",
                "outputVariableName",
                "resultVariableName",
                "listVariableName")
            ?? "list";
        var listValue = ResolveInitialListValue(context);
        SetListVariable(context, outputName, listValue, MicroflowVariableSourceKind.ActionOutput);
        started.Stop();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = listValue,
            OutputPreview = $"{outputName}[{(listValue.ValueKind == JsonValueKind.Array ? listValue.GetArrayLength() : 0)}]",
            DurationMs = (int)started.ElapsedMilliseconds
        });
    }

    private static JsonElement ResolveInitialListValue(MicroflowActionExecutionContext context)
    {
        if (context.ActionConfig.ValueKind == JsonValueKind.Object
            && context.ActionConfig.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array)
        {
            return items.Clone();
        }

        var expressionRaw = ReadExpressionRaw(ReadProperty(context.ActionConfig, "initialItemsExpression"));
        if (TryEvaluateExpression(context, expressionRaw, out var expressionValue, out _)
            && expressionValue.ValueKind == JsonValueKind.Array)
        {
            return expressionValue;
        }

        return JsonSerializer.SerializeToElement(Array.Empty<object>(), JsonOptions);
    }
}

public sealed class ChangeListActionExecutor : ListActionExecutorBase
{
    public override string ActionKind => "changeList";

    public override Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        var targetName = ReadFirstString(
            context.ActionConfig,
            "targetListVariableName",
            "targetVariableName",
            "listVariableName");
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeVariableNotFound, "ChangeList requires targetListVariableName."));
        }

        var current = ReadListFromVariable(context, targetName);
        var items = current.ValueKind == JsonValueKind.Array ? current.EnumerateArray().Select(item => item.Clone()).ToList() : [];
        var operation = (ReadFirstString(context.ActionConfig, "operation") ?? "add").Trim();
        var singleItem = ResolveSingleItem(context);
        var listItems = ResolveItemList(context);

        switch (operation)
        {
            case "add":
                if (singleItem.HasValue)
                {
                    items.Add(singleItem.Value.Clone());
                }
                break;
            case "addAll":
            case "addRange":
                items.AddRange(listItems.Select(item => item.Clone()));
                break;
            case "remove":
                if (singleItem.HasValue)
                {
                    var index = items.FindIndex(candidate => ItemsEqual(candidate, singleItem.Value));
                    if (index >= 0)
                    {
                        items.RemoveAt(index);
                    }
                }
                break;
            case "removeAll":
                if (listItems.Count > 0)
                {
                    items = items.Where(candidate => !listItems.Any(item => ItemsEqual(candidate, item))).Select(item => item.Clone()).ToList();
                }
                break;
            case "removeWhere":
                items = ApplyRemoveWhere(context, items);
                break;
            case "clear":
                items.Clear();
                break;
            case "set":
                if (TryResolveIndex(context, out var indexValue) && singleItem.HasValue)
                {
                    if (indexValue < 0 || indexValue >= items.Count)
                    {
                        return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeVariableTypeMismatch, "ChangeList set index is out of range."));
                    }

                    items[indexValue] = singleItem.Value.Clone();
                }
                else
                {
                    items = listItems.Select(item => item.Clone()).ToList();
                }
                break;
        }

        var changed = JsonSerializer.SerializeToElement(items, JsonOptions);
        SetListVariable(context, targetName!, changed, MicroflowVariableSourceKind.ActionOutput);
        started.Stop();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = changed,
            OutputPreview = $"{targetName}[{items.Count}]",
            DurationMs = (int)started.ElapsedMilliseconds
        });
    }

    private static JsonElement? ResolveSingleItem(MicroflowActionExecutionContext context)
    {
        if (TryEvaluateExpression(context, ReadExpressionRaw(ReadProperty(context.ActionConfig, "itemExpression", "valueExpression")), out var evaluated, out _))
        {
            return evaluated;
        }

        if (TryEvaluateExpression(context, ReadExpressionRaw(ReadProperty(context.ActionConfig, "appendItemExpression")), out evaluated, out _))
        {
            return evaluated;
        }

        if (context.ActionConfig.ValueKind == JsonValueKind.Object
            && context.ActionConfig.TryGetProperty("appendItem", out var appendItem))
        {
            return appendItem.Clone();
        }

        var sourceObjectVariable = ReadFirstString(context.ActionConfig, "objectVariableName");
        if (!string.IsNullOrWhiteSpace(sourceObjectVariable)
            && context.VariableStore.TryGet(sourceObjectVariable!, out var variable)
            && variable?.RawValueJson is not null
            && MicroflowVariableStore.ToJsonElement(variable.RawValueJson) is { } variableValue)
        {
            return variableValue;
        }

        return null;
    }

    private static IReadOnlyList<JsonElement> ResolveItemList(MicroflowActionExecutionContext context)
    {
        if (TryEvaluateExpression(context, ReadExpressionRaw(ReadProperty(context.ActionConfig, "itemsExpression")), out var evaluated, out _)
            && evaluated.ValueKind == JsonValueKind.Array)
        {
            return evaluated.EnumerateArray().Select(item => item.Clone()).ToArray();
        }

        var sourceListVariableName = ReadFirstString(context.ActionConfig, "sourceListVariableName");
        if (!string.IsNullOrWhiteSpace(sourceListVariableName))
        {
            return ReadListItemsFromVariable(context, sourceListVariableName);
        }

        if (context.ActionConfig.ValueKind == JsonValueKind.Object
            && context.ActionConfig.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array)
        {
            return items.EnumerateArray().Select(item => item.Clone()).ToArray();
        }

        return Array.Empty<JsonElement>();
    }

    private static bool TryResolveIndex(MicroflowActionExecutionContext context, out int index)
    {
        index = default;
        if (TryEvaluateExpression(context, ReadExpressionRaw(ReadProperty(context.ActionConfig, "indexExpression")), out var evaluated, out _))
        {
            return TryReadInt(evaluated, out index);
        }

        if (ReadProperty(context.ActionConfig, "index") is { } value)
        {
            return TryReadInt(value, out index);
        }

        return false;
    }

    private static bool ItemsEqual(JsonElement left, JsonElement right)
        => string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);

    private static List<JsonElement> ApplyRemoveWhere(MicroflowActionExecutionContext context, IReadOnlyList<JsonElement> items)
    {
        var conditionRaw = ReadExpressionRaw(ReadProperty(context.ActionConfig, "conditionExpression"));
        if (string.IsNullOrWhiteSpace(conditionRaw) || context.ExpressionEvaluator is null)
        {
            return items.Select(item => item.Clone()).ToList();
        }

        var iteratorVariableName = NormalizeVariableName(ReadFirstString(context.ActionConfig, "objectVariableName", "itemVariableName"), "item");
        var kept = new List<JsonElement>();
        foreach (var item in items)
        {
            using var scopeLease = context.RuntimeExecutionContext.PushLoopScope(
                loopObjectId: context.ObjectId,
                collectionId: context.CollectionId ?? context.ObjectId,
                iteratorVariableName: iteratorVariableName,
                index: kept.Count,
                iteratorRawValue: item,
                iteratorPreview: MicroflowVariableStore.Preview(item.GetRawText()),
                iteratorDataTypeJson: InferDataType(item).GetRawText(),
                defineIterator: true);

            if (!TryEvaluateExpression(context, conditionRaw, out var condition, out _)
                || condition.ValueKind != JsonValueKind.True)
            {
                kept.Add(item.Clone());
            }
        }

        return kept;
    }
}

public sealed class AggregateListActionExecutor : ListActionExecutorBase
{
    public override string ActionKind => "aggregateList";

    public override Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        var sourceName = ReadFirstString(
            context.ActionConfig,
            "sourceListVariableName",
            "listVariableName",
            "sourceVariableName");
        var outputName = ReadFirstString(context.ActionConfig, "outputVariableName", "resultVariableName")
            ?? "aggregateResult";
        var function = NormalizeAggregateFunction(ReadFirstString(context.ActionConfig, "aggregateFunction", "aggregate", "operation") ?? "count");
        var emptyListBehavior = ReadFirstString(context.ActionConfig, "emptyListBehavior") ?? "zero";
        var values = ResolveAggregateValues(context, sourceName);
        if (function == "count")
        {
            var countValue = JsonSerializer.SerializeToElement(values.Count, JsonOptions);
            SetVariable(
                context,
                outputName,
                countValue,
                JsonSerializer.SerializeToElement(new { kind = "integer" }, JsonOptions),
                MicroflowVariableSourceKind.ActionOutput);
            started.Stop();
            return Task.FromResult(new MicroflowActionExecutionResult
            {
                Status = MicroflowActionExecutionStatus.Success,
                OutputJson = countValue,
                OutputPreview = values.Count.ToString(CultureInfo.InvariantCulture),
                DurationMs = (int)started.ElapsedMilliseconds
            });
        }

        if (values.Count == 0)
        {
            if (string.Equals(emptyListBehavior, "error", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeVariableTypeMismatch, "AggregateList cannot aggregate an empty list when emptyListBehavior=error."));
            }

            var emptyValue = string.Equals(emptyListBehavior, "null", StringComparison.OrdinalIgnoreCase)
                ? JsonNull()
                : JsonSerializer.SerializeToElement(0m, JsonOptions);
            var emptyType = ReadProperty(context.ActionConfig, "resultType") ?? InferDataType(emptyValue);
            SetVariable(context, outputName, emptyValue, emptyType, MicroflowVariableSourceKind.ActionOutput);
            started.Stop();
            return Task.FromResult(new MicroflowActionExecutionResult
            {
                Status = MicroflowActionExecutionStatus.Success,
                OutputJson = emptyValue,
                OutputPreview = MicroflowVariableStore.Preview(emptyValue.GetRawText()),
                DurationMs = (int)started.ElapsedMilliseconds
            });
        }

        if ((function == "sum" || function == "average") && values.Any(value => !TryReadNumeric(value, out _)))
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeVariableTypeMismatch, $"AggregateList {function} requires numeric values."));
        }

        JsonElement result = function switch
        {
            "sum" => JsonSerializer.SerializeToElement(values.Sum(ReadNumeric), JsonOptions),
            "average" => JsonSerializer.SerializeToElement(values.Average(ReadNumeric), JsonOptions),
            "min" => values.MinBy(value => value, new AggregateValueComparer()).Clone(),
            "max" => values.MaxBy(value => value, new AggregateValueComparer()).Clone(),
            _ => JsonSerializer.SerializeToElement(values.Count, JsonOptions)
        };
        var resultType = ReadProperty(context.ActionConfig, "resultType") ?? InferAggregateResultType(function, result);
        SetVariable(context, outputName, result, resultType, MicroflowVariableSourceKind.ActionOutput);
        started.Stop();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = result,
            OutputPreview = MicroflowVariableStore.Preview(result.GetRawText()),
            DurationMs = (int)started.ElapsedMilliseconds
        });
    }

    private static string NormalizeAggregateFunction(string raw)
        => raw.Trim().ToLowerInvariant() switch
        {
            "minimum" => "min",
            "maximum" => "max",
            var value => value
        };

    private static IReadOnlyList<JsonElement> ResolveAggregateValues(MicroflowActionExecutionContext context, string? sourceName)
    {
        var list = ReadListItemsFromVariable(context, sourceName);
        var expressionRaw = ReadExpressionRaw(ReadProperty(context.ActionConfig, "aggregateExpression"));
        var memberPath = ReadFirstString(context.ActionConfig, "attributeQualifiedName", "member");
        var values = new List<JsonElement>(list.Count);

        foreach (var item in list)
        {
            if (!string.IsNullOrWhiteSpace(expressionRaw)
                && TryEvaluatePerItem(context, item, expressionRaw!, out var evaluated))
            {
                values.Add(evaluated);
                continue;
            }

            values.Add(ResolveMemberValue(item, memberPath));
        }

        return values.Where(value => value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Undefined).ToArray();
    }

    private static bool TryEvaluatePerItem(MicroflowActionExecutionContext context, JsonElement item, string raw, out JsonElement value)
    {
        using var scopeLease = context.RuntimeExecutionContext.PushLoopScope(
            loopObjectId: context.ObjectId,
            collectionId: context.CollectionId ?? context.ObjectId,
            iteratorVariableName: "item",
            index: 0,
            iteratorRawValue: item,
            iteratorPreview: MicroflowVariableStore.Preview(item.GetRawText()),
            iteratorDataTypeJson: InferDataType(item).GetRawText(),
            defineIterator: true);

        return TryEvaluateExpression(context, raw, out value, out _);
    }

    private static JsonElement ResolveMemberValue(JsonElement item, string? rawMemberPath)
    {
        if (string.IsNullOrWhiteSpace(rawMemberPath))
        {
            return item.Clone();
        }

        var path = rawMemberPath.Trim().TrimStart('$');
        var segments = path.Contains('/')
            ? path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            : path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return JsonNull();
        }

        if (path.Contains('/') && (segments[0].Equals("item", StringComparison.OrdinalIgnoreCase) || segments[0].Equals("current", StringComparison.OrdinalIgnoreCase)))
        {
            segments = segments[1..];
        }
        else if (!path.Contains('/') && segments.Length >= 3)
        {
            segments = [segments[^1]];
        }

        var current = item;
        foreach (var segment in segments)
        {
            if (current.ValueKind != JsonValueKind.Object || !TryGetPropertyIgnoreCase(current, segment, out var next))
            {
                return JsonNull();
            }

            current = next.Clone();
        }

        return current;
    }

    private static decimal ReadNumeric(JsonElement value)
        => TryReadNumeric(value, out var number)
            ? number
            : throw new InvalidOperationException($"Aggregate numeric operation requires number-like value, got {value.ValueKind}.");

    private static bool TryReadNumeric(JsonElement value, out decimal number)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            number = value.GetDecimal();
            return true;
        }

        if (value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number))
        {
            return true;
        }

        number = default;
        return false;
    }

    private static JsonElement InferAggregateResultType(string function, JsonElement result)
        => function switch
        {
            "sum" or "average" => JsonSerializer.SerializeToElement(new { kind = "decimal" }, JsonOptions),
            "count" => JsonSerializer.SerializeToElement(new { kind = "integer" }, JsonOptions),
            _ => InferDataType(result)
        };

    private sealed class AggregateValueComparer : IComparer<JsonElement>
    {
        public int Compare(JsonElement x, JsonElement y)
            => CompareAggregateValues(x, y);
    }

    private static int CompareAggregateValues(JsonElement left, JsonElement right)
    {
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

        return string.Compare(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
    }
}

public sealed class ListOperationActionExecutor : ListActionExecutorBase
{
    public override string ActionKind => "listOperation";

    public override Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        var operation = (ReadString(context.ActionConfig, "operation") ?? "size").Trim();
        var sourceName = ReadFirstString(
            context.ActionConfig,
            "leftListVariableName",
            "sourceListVariableName",
            "inputListVariable",
            "inputListVariableName",
            "listVariableName",
            "sourceVariableName",
            "leftVariableName");
        var outputName = ReadFirstString(context.ActionConfig, "outputListVariableName", "outputVariableName", "resultVariableName")
            ?? $"{operation}Result";
        var source = ReadListItems(context, sourceName);
        var other = ReadOtherItems(context);
        var result = operation switch
        {
            "union" => JsonSerializer.SerializeToElement(DistinctByJson(source.Concat(other)), JsonOptions),
            "intersect" => JsonSerializer.SerializeToElement(source.Where(item => ContainsJson(other, item)).Select(item => item.Clone()).ToArray(), JsonOptions),
            "subtract" => JsonSerializer.SerializeToElement(source.Where(item => !ContainsJson(other, item)).Select(item => item.Clone()).ToArray(), JsonOptions),
            "equals" => JsonSerializer.SerializeToElement(JsonArraysEqual(source, other), JsonOptions),
            "distinct" => JsonSerializer.SerializeToElement(DistinctByJson(source), JsonOptions),
            "contains" => JsonSerializer.SerializeToElement(ContainsJson(source, ReadItem(context, context.ActionConfig)), JsonOptions),
            "isEmpty" => JsonSerializer.SerializeToElement(source.Count == 0, JsonOptions),
            "head" or "first" => source.Count > 0 ? source[0].Clone() : JsonNull(),
            "tail" => JsonSerializer.SerializeToElement(source.Skip(1).Select(item => item.Clone()).ToArray(), JsonOptions),
            "find" => FindByValue(source, ReadItem(context, context.ActionConfig)),
            "last" => source.Count > 0 ? source[^1].Clone() : JsonNull(),
            "reverse" => JsonSerializer.SerializeToElement(source.AsEnumerable().Reverse().Select(item => item.Clone()).ToArray(), JsonOptions),
            "size" => JsonSerializer.SerializeToElement(source.Count, JsonOptions),
            "filter" => ExecuteFilter(context, source),
            "sort" => ExecuteSort(context, source),
            "map" => ExecuteMap(context, source),
            "take" => JsonSerializer.SerializeToElement(source.Take(ReadLimit(context)).Select(item => item.Clone()).ToArray(), JsonOptions),
            "skip" => JsonSerializer.SerializeToElement(source.Skip(ReadOffset(context)).Select(item => item.Clone()).ToArray(), JsonOptions),
            _ => default
        };

        if (result.ValueKind == JsonValueKind.Undefined)
        {
            started.Stop();
            return Task.FromResult(new MicroflowActionExecutionResult
            {
                Status = MicroflowActionExecutionStatus.Failed,
                Error = new MicroflowRuntimeErrorDto
                {
                    Code = RuntimeErrorCode.RuntimeUnsupportedAction,
                    Message = $"List Operation does not support operation '{operation}'.",
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId
                },
                Message = $"List Operation does not support operation '{operation}'.",
                ShouldContinueNormalFlow = false,
                ShouldStopRun = true,
                DurationMs = (int)started.ElapsedMilliseconds
            });
        }

        SetOutputVariable(context, outputName, result, operation);
        started.Stop();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = result,
            OutputPreview = $"{operation} => {MicroflowVariableStore.Preview(result.GetRawText())}",
            DurationMs = (int)started.ElapsedMilliseconds
        });
    }

    private static List<JsonElement> ReadListItems(MicroflowActionExecutionContext context, string? variableName)
    {
        var list = ReadListFromVariable(context, variableName);
        return list.ValueKind == JsonValueKind.Array
            ? list.EnumerateArray().Select(item => item.Clone()).ToList()
            : [];
    }

    private static List<JsonElement> ReadOtherItems(MicroflowActionExecutionContext context)
    {
        var otherName = ReadFirstString(
            context.ActionConfig,
            "rightListVariableName",
            "otherListVariableName",
            "secondListVariable",
            "secondListVariableName",
            "rightVariableName");
        var other = ReadListItems(context, otherName);
        if (other.Count > 0)
        {
            return other;
        }

        if (context.ActionConfig.ValueKind == JsonValueKind.Object
            && context.ActionConfig.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array)
        {
            return items.EnumerateArray().Select(item => item.Clone()).ToList();
        }

        return [];
    }

    private static JsonElement ReadItem(MicroflowActionExecutionContext context, JsonElement config)
    {
        if (TryEvaluateExpression(context, ReadExpressionRaw(ReadProperty(config, "itemExpression")), out var evaluated, out _))
        {
            return evaluated;
        }

        if (config.ValueKind == JsonValueKind.Object)
        {
            var itemVariableName = ReadString(config, "itemVariable") ?? ReadString(config, "itemVariableName");
            if (!string.IsNullOrWhiteSpace(itemVariableName)
                && context.VariableStore.TryGet(itemVariableName!, out var variable)
                && variable?.RawValueJson is not null
                && MicroflowVariableStore.ToJsonElement(variable.RawValueJson) is { } variableValue)
            {
                return variableValue;
            }

            if (config.TryGetProperty("item", out var item))
            {
                return item.Clone();
            }

            if (config.TryGetProperty("value", out var value))
            {
                return value.Clone();
            }
        }

        return JsonNull();
    }

    private static JsonElement ExecuteFilter(MicroflowActionExecutionContext context, IReadOnlyList<JsonElement> source)
    {
        var expressionRaw = ReadExpressionRaw(ReadProperty(context.ActionConfig, "filterExpression", "expression"));
        if (string.IsNullOrWhiteSpace(expressionRaw) || context.ExpressionEvaluator is null)
        {
            return JsonSerializer.SerializeToElement(source.Select(item => item.Clone()).ToArray(), JsonOptions);
        }

        var itemVariableName = NormalizeVariableName(ReadFirstString(context.ActionConfig, "objectVariableName", "itemVariableName"), "item");
        var filtered = new List<JsonElement>();
        for (var index = 0; index < source.Count; index++)
        {
            var item = source[index];
            using var scopeLease = context.RuntimeExecutionContext.PushLoopScope(
                loopObjectId: context.ObjectId,
                collectionId: context.CollectionId ?? context.ObjectId,
                iteratorVariableName: itemVariableName,
                index: index,
                iteratorRawValue: item,
                iteratorPreview: MicroflowVariableStore.Preview(item.GetRawText()),
                iteratorDataTypeJson: InferDataType(item).GetRawText(),
                defineIterator: true);

            if (TryEvaluateExpression(context, expressionRaw, out var condition, out _)
                && condition.ValueKind == JsonValueKind.True)
            {
                filtered.Add(item.Clone());
            }
        }

        return JsonSerializer.SerializeToElement(filtered, JsonOptions);
    }

    private static JsonElement ExecuteSort(MicroflowActionExecutionContext context, IReadOnlyList<JsonElement> source)
    {
        var items = source.Select(item => item.Clone()).ToList();
        var sortKeys = ReadSortKeys(context.ActionConfig);
        if (sortKeys.Count == 0)
        {
            var fallbackExpression = ReadExpressionRaw(ReadProperty(context.ActionConfig, "sortExpression"));
            if (!string.IsNullOrWhiteSpace(fallbackExpression))
            {
                sortKeys.Add(new SortKeySpec(null, "asc", fallbackExpression));
            }
            else
            {
                var fallbackField = ReadFirstString(context.ActionConfig, "sortField", "memberName");
                if (!string.IsNullOrWhiteSpace(fallbackField))
                {
                    sortKeys.Add(new SortKeySpec(fallbackField!, "asc", null));
                }
            }
        }

        if (sortKeys.Count == 0)
        {
            return JsonSerializer.SerializeToElement(items, JsonOptions);
        }

        var itemVariableName = NormalizeVariableName(ReadFirstString(context.ActionConfig, "objectVariableName", "itemVariableName", "itemVariable"), "item");
        items.Sort((left, right) =>
        {
            foreach (var sortKey in sortKeys)
            {
                var leftValue = ResolveSortValue(context, left, sortKey, itemVariableName);
                var rightValue = ResolveSortValue(context, right, sortKey, itemVariableName);
                var comparison = CompareJsonElements(leftValue, rightValue);
                if (comparison != 0)
                {
                    return string.Equals(sortKey.Direction, "desc", StringComparison.OrdinalIgnoreCase) ? -comparison : comparison;
                }
            }

            return 0;
        });

        return JsonSerializer.SerializeToElement(items, JsonOptions);
    }

    private static JsonElement ExtractSortValue(JsonElement element, string? sortField)
    {
        if (string.IsNullOrWhiteSpace(sortField))
        {
            return element;
        }

        if (element.ValueKind == JsonValueKind.Object && TryGetPropertyIgnoreCase(element, sortField, out var value))
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

        return string.Compare(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);
    }

    private static JsonElement ExecuteMap(MicroflowActionExecutionContext context, IReadOnlyList<JsonElement> source)
    {
        var expressionRaw = ReadExpressionRaw(ReadProperty(context.ActionConfig, "expression", "mapExpression"));
        if (string.IsNullOrWhiteSpace(expressionRaw) || context.ExpressionEvaluator is null)
        {
            return JsonSerializer.SerializeToElement(source.Select(item => item.Clone()).ToArray(), JsonOptions);
        }

        var itemVariableName = NormalizeVariableName(ReadFirstString(context.ActionConfig, "objectVariableName", "itemVariableName"), "item");
        var output = new List<JsonElement>(source.Count);
        for (var index = 0; index < source.Count; index++)
        {
            var item = source[index];
            using var scopeLease = context.RuntimeExecutionContext.PushLoopScope(
                loopObjectId: context.ObjectId,
                collectionId: context.CollectionId ?? context.ObjectId,
                iteratorVariableName: itemVariableName,
                index: index,
                iteratorRawValue: item,
                iteratorPreview: MicroflowVariableStore.Preview(item.GetRawText()),
                iteratorDataTypeJson: InferDataType(item).GetRawText(),
                defineIterator: true);

            if (TryEvaluateExpression(context, expressionRaw, out var mapped, out _))
            {
                output.Add(mapped);
            }
        }

        return JsonSerializer.SerializeToElement(output, JsonOptions);
    }

    private static int ReadLimit(MicroflowActionExecutionContext context)
        => ReadProperty(context.ActionConfig, "limit") is { } value && TryReadInt(value, out var limit)
            ? Math.Max(limit, 0)
            : int.MaxValue;

    private static int ReadOffset(MicroflowActionExecutionContext context)
        => ReadProperty(context.ActionConfig, "offset") is { } value && TryReadInt(value, out var offset)
            ? Math.Max(offset, 0)
            : 0;

    private static JsonElement ResolveSortValue(
        MicroflowActionExecutionContext context,
        JsonElement item,
        SortKeySpec sortKey,
        string itemVariableName)
    {
        if (string.IsNullOrWhiteSpace(sortKey.ExpressionRaw))
        {
            return ExtractSortValue(item, sortKey.Field);
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
        return TryEvaluateExpression(context, sortKey.ExpressionRaw, out var evaluated, out _)
            ? evaluated
            : JsonNull();
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

    private static JsonElement FindByValue(IReadOnlyList<JsonElement> source, JsonElement value)
    {
        var found = source.FirstOrDefault(item => JsonEquals(item, value));
        return found.ValueKind == JsonValueKind.Undefined ? JsonNull() : found.Clone();
    }

    private static JsonElement[] DistinctByJson(IEnumerable<JsonElement> items)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var output = new List<JsonElement>();
        foreach (var item in items)
        {
            var key = NormalizeJson(item);
            if (seen.Add(key))
            {
                output.Add(item.Clone());
            }
        }

        return output.ToArray();
    }

    private static bool ContainsJson(IEnumerable<JsonElement> items, JsonElement value)
        => items.Any(item => JsonEquals(item, value));

    private static bool JsonArraysEqual(IReadOnlyList<JsonElement> left, IReadOnlyList<JsonElement> right)
        => left.Count == right.Count && left.Zip(right).All(pair => JsonEquals(pair.First, pair.Second));

    private static bool JsonEquals(JsonElement left, JsonElement right)
        => string.Equals(NormalizeJson(left), NormalizeJson(right), StringComparison.Ordinal);

    private static string NormalizeJson(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => JsonSerializer.Serialize(element.EnumerateObject()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToDictionary(property => property.Name, property => JsonDocument.Parse(NormalizeJson(property.Value)).RootElement.Clone()), JsonOptions),
            _ => element.GetRawText()
        };

    private static void SetOutputVariable(MicroflowActionExecutionContext context, string name, JsonElement value, string operation)
    {
        var dataType = value.ValueKind == JsonValueKind.Array && ReadProperty(context.ActionConfig, "outputElementType") is { } outputElementType
            ? JsonSerializer.SerializeToElement(new { kind = "list", itemType = outputElementType }, JsonOptions)
            : InferDataType(value);
        SetVariable(context, name, value, dataType, MicroflowVariableSourceKind.ActionOutput);
    }
}
