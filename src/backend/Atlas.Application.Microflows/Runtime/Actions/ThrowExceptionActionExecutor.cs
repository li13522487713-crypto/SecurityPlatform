using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Expressions;

namespace Atlas.Application.Microflows.Runtime.Actions;

/// <summary>
/// 主动抛出运行时错误并中止微流；可作为 P0 错误处理节点的服务端语义。
/// </summary>
public sealed class ThrowExceptionActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ActionKind => "throwException";

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public Task<MicroflowActionExecutionResult> ExecuteAsync(
        MicroflowActionExecutionContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var started = Stopwatch.StartNew();
        var code = ReadString(context.ActionConfig, "errorCode") ?? "MF_THROWN_EXCEPTION";
        var messageExpression = ReadString(context.ActionConfig, "messageExpression");
        var messageLiteral = ReadString(context.ActionConfig, "message")
            ?? ReadString(context.ActionConfig, "errorMessage");
        var severity = (ReadString(context.ActionConfig, "severity") ?? "error").ToLowerInvariant();

        var resolvedMessage = messageLiteral ?? "Microflow threw an exception.";
        if (!string.IsNullOrWhiteSpace(messageExpression) && context.ExpressionEvaluator is not null)
        {
            var evaluation = context.ExpressionEvaluator.Evaluate(
                messageExpression!,
                new MicroflowExpressionEvaluationContext
                {
                    RuntimeExecutionContext = context.RuntimeExecutionContext,
                    VariableStore = context.VariableStore,
                    MetadataCatalog = context.MetadataCatalog,
                    CurrentObjectId = context.ObjectId,
                    CurrentActionId = context.ActionId,
                    Mode = MicroflowRuntimeExecutionMode.TestRun
                });
            if (evaluation.Success && !string.IsNullOrEmpty(evaluation.RawValueJson))
            {
                var element = MicroflowVariableStore.ToJsonElement(evaluation.RawValueJson) ?? default;
                resolvedMessage = element.ValueKind == JsonValueKind.String
                    ? element.GetString() ?? resolvedMessage
                    : evaluation.RawValueJson;
            }
            else if (!evaluation.Success && evaluation.Error is not null)
            {
                resolvedMessage = $"{messageLiteral ?? string.Empty} (expression error: {evaluation.Error.Message})";
            }
        }

        var error = new MicroflowRuntimeErrorDto
        {
            Code = code,
            Message = resolvedMessage,
            ObjectId = context.ObjectId,
            ActionId = context.ActionId,
            Details = JsonSerializer.Serialize(new { actionKind = ActionKind, severity }, JsonOptions)
        };

        started.Stop();
        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Failed,
            Error = error,
            Message = resolvedMessage,
            ShouldContinueNormalFlow = false,
            ShouldEnterErrorHandler = true,
            ShouldStopRun = true,
            DurationMs = (int)started.ElapsedMilliseconds,
            Diagnostics =
            [
                new MicroflowActionExecutionDiagnostic
                {
                    Code = code,
                    Severity = severity,
                    Message = resolvedMessage,
                    ActionKind = ActionKind,
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId
                }
            ]
        });
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
}
