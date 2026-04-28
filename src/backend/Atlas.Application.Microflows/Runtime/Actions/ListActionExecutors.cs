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
