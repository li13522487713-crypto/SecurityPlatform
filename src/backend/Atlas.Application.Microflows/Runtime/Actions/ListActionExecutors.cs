using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Microflows.Models;

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
        var outputName = ReadString(context.ActionConfig, "outputVariableName")
            ?? ReadString(context.ActionConfig, "resultVariableName")
            ?? "list";
        var listValue = context.ActionConfig.ValueKind == JsonValueKind.Object && context.ActionConfig.TryGetProperty("items", out var items)
            ? items.Clone()
            : JsonSerializer.SerializeToElement(Array.Empty<object>(), JsonOptions);
        SetListVariable(context, outputName, listValue, MicroflowVariableSourceKind.ActionOutput);
        started.Stop();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = listValue,
            OutputPreview = $"{outputName}[]",
            DurationMs = (int)started.ElapsedMilliseconds
        });
    }
}

public sealed class ChangeListActionExecutor : ListActionExecutorBase
{
    public override string ActionKind => "changeList";

    public override Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        var targetName = ReadString(context.ActionConfig, "targetVariableName")
            ?? ReadString(context.ActionConfig, "listVariableName");
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeVariableNotFound, "ChangeList requires targetVariableName."));
        }

        var current = ReadListFromVariable(context, targetName);
        var items = current.ValueKind == JsonValueKind.Array ? current.EnumerateArray().Select(item => item.Clone()).ToList() : [];
        if (context.ActionConfig.ValueKind == JsonValueKind.Object && context.ActionConfig.TryGetProperty("appendItem", out var appendItem))
        {
            items.Add(appendItem.Clone());
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
}

public sealed class AggregateListActionExecutor : ListActionExecutorBase
{
    public override string ActionKind => "aggregateList";

    public override Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        var sourceName = ReadString(context.ActionConfig, "listVariableName")
            ?? ReadString(context.ActionConfig, "sourceVariableName");
        var outputName = ReadString(context.ActionConfig, "outputVariableName")
            ?? ReadString(context.ActionConfig, "resultVariableName")
            ?? "aggregateResult";
        var list = ReadListFromVariable(context, sourceName);
        var count = list.ValueKind == JsonValueKind.Array ? list.GetArrayLength() : 0;
        var value = JsonSerializer.SerializeToElement(count, JsonOptions);
        context.VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = outputName,
            DataTypeJson = JsonSerializer.Serialize(new { kind = "integer" }, JsonOptions),
            RawValueJson = value.GetRawText(),
            ValuePreview = count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SourceKind = MicroflowVariableSourceKind.ActionOutput,
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId,
            ScopeKind = MicroflowVariableScopeKind.Action,
            AllowShadowing = true
        });
        started.Stop();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = value,
            OutputPreview = count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DurationMs = (int)started.ElapsedMilliseconds
        });
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
        var sourceName = ReadString(context.ActionConfig, "listVariableName")
            ?? ReadString(context.ActionConfig, "sourceVariableName")
            ?? ReadString(context.ActionConfig, "leftVariableName");
        var outputName = ReadString(context.ActionConfig, "outputVariableName")
            ?? ReadString(context.ActionConfig, "resultVariableName")
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
            "contains" => JsonSerializer.SerializeToElement(ContainsJson(source, ReadItem(context.ActionConfig)), JsonOptions),
            "isEmpty" => JsonSerializer.SerializeToElement(source.Count == 0, JsonOptions),
            "head" or "first" => source.Count > 0 ? source[0].Clone() : JsonNull(),
            "tail" => JsonSerializer.SerializeToElement(source.Skip(1).Select(item => item.Clone()).ToArray(), JsonOptions),
            "find" => FindByValue(source, ReadItem(context.ActionConfig)),
            "last" => source.Count > 0 ? source[^1].Clone() : JsonNull(),
            "reverse" => JsonSerializer.SerializeToElement(source.AsEnumerable().Reverse().Select(item => item.Clone()).ToArray(), JsonOptions),
            "size" => JsonSerializer.SerializeToElement(source.Count, JsonOptions),
            _ => JsonSerializer.SerializeToElement(source.Count, JsonOptions)
        };

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
        var otherName = ReadString(context.ActionConfig, "otherListVariableName")
            ?? ReadString(context.ActionConfig, "rightVariableName");
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

    private static JsonElement ReadItem(JsonElement config)
    {
        if (config.ValueKind == JsonValueKind.Object)
        {
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

    private static JsonElement FindByValue(IReadOnlyList<JsonElement> source, JsonElement value)
    {
        var found = source.FirstOrDefault(item => JsonEquals(item, value));
        return found.ValueKind == JsonValueKind.Undefined ? JsonNull() : found.Clone();
    }

    private static JsonElement JsonNull()
        => JsonSerializer.SerializeToElement<object?>(null, JsonOptions);

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
        var kind = value.ValueKind switch
        {
            JsonValueKind.Array => "list",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Number => "integer",
            JsonValueKind.Object => "object",
            _ => "unknown"
        };
        var dataTypeJson = JsonSerializer.Serialize(new { kind }, JsonOptions);
        var rawValueJson = value.GetRawText();
        var variable = new MicroflowRuntimeVariableValue
        {
            Name = name,
            DataTypeJson = dataTypeJson,
            RawValueJson = rawValueJson,
            ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
            SourceKind = MicroflowVariableSourceKind.ActionOutput,
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
            ValuePreview = $"{operation}: {variable.ValuePreview}",
            SourceKind = MicroflowVariableSourceKind.ActionOutput,
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId,
            ScopeKind = MicroflowVariableScopeKind.Action,
            Value = variable,
            AllowShadowing = true
        });
    }
}
