using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Expressions;

namespace Atlas.Application.Microflows.Runtime.Actions;

public sealed class CreateVariableActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ActionKind => "createVariable";

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        var variableName = ReadString(context.ActionConfig, "variableName");
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeVariableNotFound, "Create Variable requires variableName."));
        }

        var dataTypeJson = context.ActionConfig.TryGetProperty("dataType", out var dataType)
            ? dataType.GetRawText()
            : JsonSerializer.Serialize(new { kind = "unknown" }, JsonOptions);
        var expression = ReadExpressionText(context.ActionConfig, "initialValue")
            ?? ReadExpressionText(context.ActionConfig, "initialValueExpression");
        var rawValueJson = string.IsNullOrWhiteSpace(expression)
            ? "null"
            : EvaluateExpression(context, expression!);
        var value = MicroflowVariableStore.ToJsonElement(rawValueJson) ?? JsonSerializer.SerializeToElement<object?>(null, JsonOptions);

        context.VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = variableName!,
            DataTypeJson = dataTypeJson,
            RawValueJson = rawValueJson,
            ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
            SourceKind = MicroflowVariableSourceKind.LocalVariable,
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId,
            ScopeKind = MicroflowVariableScopeKind.Action
        });

        started.Stop();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = JsonSerializer.SerializeToElement(new { variableName, value }, JsonOptions),
            OutputPreview = $"{variableName}={MicroflowVariableStore.Preview(rawValueJson)}",
            ProducedVariables =
            [
                new MicroflowRuntimeVariableValueDto
                {
                    Name = variableName!,
                    Type = MicroflowVariableStore.ToJsonElement(dataTypeJson),
                    RawValue = value,
                    RawValueJson = rawValueJson,
                    ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
                    Source = MicroflowVariableSourceKind.LocalVariable,
                    ScopeKind = MicroflowVariableScopeKind.Action
                }
            ],
            DurationMs = (int)started.ElapsedMilliseconds
        });
    }

    private static MicroflowActionExecutionResult Failed(Stopwatch started, string code, string message)
    {
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Failed,
            Error = new MicroflowRuntimeErrorDto { Code = code, Message = message },
            Message = message,
            DurationMs = (int)started.ElapsedMilliseconds,
            ShouldContinueNormalFlow = false,
            ShouldStopRun = true
        };
    }

    internal static string EvaluateExpression(MicroflowActionExecutionContext context, string expression)
    {
        if (context.ExpressionEvaluator is null)
        {
            throw new InvalidOperationException("ExpressionEvaluator is required for variable actions.");
        }

        var result = context.ExpressionEvaluator.Evaluate(
            expression,
            new MicroflowExpressionEvaluationContext
            {
                RuntimeExecutionContext = context.RuntimeExecutionContext,
                VariableStore = context.VariableStore,
                MetadataCatalog = context.MetadataCatalog,
                MetadataResolver = context.MetadataResolver,
                CurrentObjectId = context.ObjectId,
                CurrentActionId = context.ActionId,
                CurrentCollectionId = context.CollectionId,
                Mode = context.Options.Mode,
                Options = new MicroflowExpressionEvaluationOptions
                {
                    AllowUnknownVariables = false,
                    AllowUnsupportedFunctions = false,
                    StrictTypeCheck = true,
                    MaxEvaluationDepth = 64,
                    MaxStringLength = 500
                }
            });
        if (!result.Success || result.RawValueJson is null)
        {
            throw new InvalidOperationException(result.Error?.Message ?? "Expression evaluation failed.");
        }

        return result.RawValueJson;
    }

    internal static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    internal static string? ReadExpressionText(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : value.GetRawText();
        }

        return ReadString(value, "raw") ?? ReadString(value, "text") ?? ReadString(value, "expression");
    }
}

public sealed class ChangeVariableActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ActionKind => "changeVariable";

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        var variableName = CreateVariableActionExecutor.ReadString(context.ActionConfig, "targetVariableName")
            ?? CreateVariableActionExecutor.ReadString(context.ActionConfig, "variableName");
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeVariableNotFound, "Change Variable requires targetVariableName."));
        }

        if (!context.VariableStore.TryGet(variableName!, out var current) || current is null)
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeVariableNotFound, $"Variable '{variableName}' was not found."));
        }

        var expression = CreateVariableActionExecutor.ReadExpressionText(context.ActionConfig, "newValueExpression")
            ?? CreateVariableActionExecutor.ReadExpressionText(context.ActionConfig, "valueExpression");
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeExpressionError, $"Change Variable '{variableName}' requires newValueExpression."));
        }

        var rawValueJson = CreateVariableActionExecutor.EvaluateExpression(context, expression!);
        var rawValue = MicroflowVariableStore.ToJsonElement(rawValueJson) ?? JsonSerializer.SerializeToElement<object?>(null, JsonOptions);
        context.VariableStore.Set(
            variableName!,
            current with
            {
                RawValueJson = rawValueJson,
                ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
                SourceKind = MicroflowVariableSourceKind.LocalVariable,
                SourceObjectId = context.ObjectId,
                SourceActionId = context.ActionId
            });

        started.Stop();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = JsonSerializer.SerializeToElement(new { variableName, value = rawValue }, JsonOptions),
            OutputPreview = $"{variableName}={MicroflowVariableStore.Preview(rawValueJson)}",
            ProducedVariables =
            [
                new MicroflowRuntimeVariableValueDto
                {
                    Name = variableName!,
                    Type = MicroflowVariableStore.ToJsonElement(current.DataTypeJson),
                    RawValue = rawValue,
                    RawValueJson = rawValueJson,
                    ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
                    Source = MicroflowVariableSourceKind.LocalVariable,
                    ScopeKind = current.ScopeKind
                }
            ],
            DurationMs = (int)started.ElapsedMilliseconds
        });
    }

    private static MicroflowActionExecutionResult Failed(Stopwatch started, string code, string message)
    {
        started.Stop();
        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Failed,
            Error = new MicroflowRuntimeErrorDto { Code = code, Message = message },
            Message = message,
            DurationMs = (int)started.ElapsedMilliseconds,
            ShouldContinueNormalFlow = false,
            ShouldStopRun = true
        };
    }
}
