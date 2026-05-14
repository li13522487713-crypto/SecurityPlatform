using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Expressions;

namespace Atlas.Application.Microflows.Runtime.Actions.Database;

/// <summary>
/// 执行 declareLocalVariable 动作，支持 scope(local/global)、多种赋值来源与数据类型。
/// 替代旧版 createVariable，兼容读取旧版字段（backward compatible）。
/// </summary>
public sealed class DeclareLocalVariableActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ActionKind => "declareLocalVariable";

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();

        var variableName = ReadString(context.ActionConfig, "variableName");
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeVariableNotFound, "局部变量节点缺少 variableName。"));
        }

        var scope = ReadString(context.ActionConfig, "scope") ?? "local";
        var scopeKind = string.Equals(scope, "global", StringComparison.OrdinalIgnoreCase)
            ? MicroflowVariableScopeKind.Global
            : MicroflowVariableScopeKind.Action;

        var dataTypeJson = context.ActionConfig.TryGetProperty("dataType", out var dataType)
            ? dataType.GetRawText()
            : JsonSerializer.Serialize(new { kind = "unknown" }, JsonOptions);

        var source = ReadString(context.ActionConfig, "source") ?? "empty";

        string rawValueJson;
        try
        {
            rawValueJson = ResolveValue(context, source);
        }
        catch (Exception ex)
        {
            return Task.FromResult(Failed(started, RuntimeErrorCode.RuntimeExpressionError, ex.Message));
        }

        var value = MicroflowVariableStore.ToJsonElement(rawValueJson)
            ?? JsonSerializer.SerializeToElement<object?>(null, JsonOptions);

        context.VariableStore.Define(new MicroflowVariableDefinition
        {
            Name = variableName!,
            DataTypeJson = dataTypeJson,
            RawValueJson = rawValueJson,
            ValuePreview = MicroflowVariableStore.Preview(rawValueJson),
            SourceKind = MicroflowVariableSourceKind.LocalVariable,
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId,
            ScopeKind = scopeKind,
            AllowRedeclare = scopeKind == MicroflowVariableScopeKind.Global
        });

        started.Stop();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = JsonSerializer.SerializeToElement(new { variableName, value }, JsonOptions),
            OutputPreview = $"{variableName}={MicroflowVariableStore.Preview(rawValueJson)} (scope={scope})",
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
                    ScopeKind = scopeKind
                }
            ],
            DurationMs = (int)started.ElapsedMilliseconds
        });
    }

    private static string ResolveValue(MicroflowActionExecutionContext context, string source)
    {
        switch (source.ToLowerInvariant())
        {
            case "literal":
            {
                var literalProp = ReadString(context.ActionConfig, "value")
                    ?? ReadString(context.ActionConfig, "literalValue");
                return literalProp is null ? "null" : JsonSerializer.Serialize(literalProp, JsonOptions);
            }

            case "expression":
            {
                var expression = CreateVariableActionExecutor.ReadExpressionText(context.ActionConfig, "expression")
                    ?? CreateVariableActionExecutor.ReadExpressionText(context.ActionConfig, "initialValue")
                    ?? CreateVariableActionExecutor.ReadExpressionText(context.ActionConfig, "initialValueExpression");
                if (string.IsNullOrWhiteSpace(expression))
                {
                    return "null";
                }

                return CreateVariableActionExecutor.EvaluateExpression(context, expression!);
            }

            case "reference":
            {
                var refName = ReadString(context.ActionConfig, "reference")
                    ?? ReadString(context.ActionConfig, "referenceVariable");
                if (string.IsNullOrWhiteSpace(refName))
                {
                    return "null";
                }

                return context.VariableStore.TryGet(refName!, out var refVar) && refVar is not null
                    ? refVar.RawValueJson ?? "null"
                    : "null";
            }

            default: // "empty"
                return "null";
        }
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

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
}
