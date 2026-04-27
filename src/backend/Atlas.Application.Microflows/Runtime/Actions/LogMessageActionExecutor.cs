using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Services;

namespace Atlas.Application.Microflows.Runtime.Actions;

public sealed partial class LogMessageActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ValidLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "trace",
        "debug",
        "info",
        "warning",
        "error",
        "critical"
    };

    public string ActionKind => "logMessage";

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public Task<MicroflowActionExecutionResult> ExecuteAsync(
        MicroflowActionExecutionContext context,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (context.ExpressionEvaluator is null)
        {
            return Task.FromResult(Failed(context, RuntimeErrorCode.RuntimeExpressionError, "LogMessage requires ExpressionEvaluator."));
        }

        var template = ReadObject(context.ActionConfig, "template");
        var text = ReadString(template, "text") ?? ReadString(context.ActionConfig, "text");
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(Failed(context, RuntimeErrorCode.RuntimeLogMessageFailed, "LogMessage template.text is required."));
        }

        var level = (ReadString(context.ActionConfig, "level") ?? ReadString(template, "level") ?? "info").Trim().ToLowerInvariant();
        if (!ValidLevels.Contains(level))
        {
            return Task.FromResult(Failed(context, RuntimeErrorCode.RuntimeLogMessageFailed, $"Unknown LogMessage level '{level}'."));
        }

        var arguments = EvaluateArguments(context, template, out var expressionError);
        if (expressionError is not null && !ReadBool(context.ActionConfig, "logExpressionErrorsAsWarning"))
        {
            return Task.FromResult(Failed(context, expressionError.Code, expressionError.Message, expressionError.Details));
        }

        var diagnostics = new List<MicroflowActionExecutionDiagnostic>();
        if (expressionError is not null)
        {
            diagnostics.Add(new MicroflowActionExecutionDiagnostic
            {
                Code = expressionError.Code,
                Severity = "warning",
                Message = expressionError.Message,
                ActionKind = context.ActionKind,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId
            });
        }

        var message = FormatTemplate(text!, arguments.Select(argument => argument.ValuePreview).ToArray(), diagnostics, context);
        var includeTraceId = ReadBool(context.ActionConfig, "includeTraceId");
        var variablesPreview = ReadBool(context.ActionConfig, "includeContextVariables")
            ? BuildVariablesPreview(context)
            : null;
        var logNodeName = ReadString(context.ActionConfig, "logNodeName");
        var traceId = includeTraceId ? context.RuntimeExecutionContext.RunId : null;
        var structuredFields = JsonSerializer.Serialize(new
        {
            kind = "logMessage",
            template = text,
            arguments = arguments.Select((argument, index) => new { index, argument.ValuePreview, argument.RawValueJson }).ToArray(),
            includeTraceId,
            logNodeName,
            variablesPreview
        }, JsonOptions);

        var log = new MicroflowRuntimeLogDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            ObjectId = context.ObjectId,
            ActionId = context.ActionId,
            LogNodeName = logNodeName,
            TraceId = traceId,
            Message = message,
            VariablesPreview = variablesPreview,
            StructuredFieldsJson = structuredFields
        };

        var output = JsonSerializer.SerializeToElement(new
        {
            logMessage = new
            {
                logLevel = level,
                logNodeName,
                messagePreview = MicroflowVariableStore.TrimPreview(message, 300),
                argumentPreviews = arguments.Select(argument => argument.ValuePreview).ToArray(),
                includeTraceId,
                includeContextVariables = variablesPreview.HasValue
            }
        }, JsonOptions);

        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = output,
            OutputPreview = message,
            Logs = [log],
            Diagnostics = diagnostics,
            DurationMs = 0,
            ShouldContinueNormalFlow = true,
            Message = message
        });
    }

    private static IReadOnlyList<MicroflowExpressionEvaluationResult> EvaluateArguments(
        MicroflowActionExecutionContext context,
        JsonElement template,
        out MicroflowRuntimeErrorDto? error)
    {
        error = null;
        if (template.ValueKind != JsonValueKind.Object
            || !template.TryGetProperty("arguments", out var arguments)
            || arguments.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MicroflowExpressionEvaluationResult>();
        }

        var results = new List<MicroflowExpressionEvaluationResult>();
        foreach (var argument in arguments.EnumerateArray())
        {
            var raw = ReadExpressionText(argument);
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var result = context.ExpressionEvaluator!.Evaluate(
                raw!,
                new MicroflowExpressionEvaluationContext
                {
                    RuntimeExecutionContext = context.RuntimeExecutionContext,
                    VariableStore = context.VariableStore,
                    MetadataCatalog = context.MetadataCatalog,
                    MetadataResolver = context.MetadataResolver,
                    CurrentObjectId = context.ObjectId,
                    CurrentActionId = context.ActionId,
                    CurrentCollectionId = context.CollectionId,
                    ExpectedType = null,
                    Mode = context.Options.Mode,
                    Options = new MicroflowExpressionEvaluationOptions
                    {
                        AllowUnknownVariables = false,
                        AllowUnsupportedFunctions = false,
                        StrictTypeCheck = true,
                        MaxStringLength = 500
                    }
                });
            if (!result.Success)
            {
                error = new MicroflowRuntimeErrorDto
                {
                    Code = result.Error?.Code ?? RuntimeErrorCode.RuntimeExpressionError,
                    Message = result.Error?.Message ?? "LogMessage argument expression failed.",
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId,
                    Details = JsonSerializer.Serialize(result.Diagnostics, JsonOptions)
                };
                return results;
            }

            results.Add(result);
        }

        return results;
    }

    private static string FormatTemplate(
        string template,
        IReadOnlyList<string> arguments,
        List<MicroflowActionExecutionDiagnostic> diagnostics,
        MicroflowActionExecutionContext context)
    {
        var message = template;
        for (var index = 0; index < arguments.Count; index++)
        {
            message = message.Replace("{" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}", arguments[index], StringComparison.Ordinal);
        }

        foreach (Match match in PlaceholderRegex().Matches(template))
        {
            if (int.TryParse(match.Groups[1].Value, out var index) && index >= arguments.Count)
            {
                diagnostics.Add(new MicroflowActionExecutionDiagnostic
                {
                    Code = RuntimeErrorCode.RuntimeLogMessageFailed,
                    Severity = "warning",
                    Message = $"LogMessage template placeholder {{{index}}} has no matching argument.",
                    ActionKind = context.ActionKind,
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId
                });
            }
        }

        return message;
    }

    private static JsonElement? BuildVariablesPreview(MicroflowActionExecutionContext context)
    {
        var variables = context.VariableStore.CurrentVariables.Values
            .Where(variable => !variable.System)
            .OrderBy(variable => variable.Name, StringComparer.Ordinal)
            .Take(20)
            .Select(variable => new
            {
                variable.Name,
                variable.Kind,
                variable.ScopeKind,
                valuePreview = MicroflowVariableStore.TrimPreview(variable.ValuePreview, 120),
                variable.SourceKind
            })
            .ToArray();

        return JsonSerializer.SerializeToElement(variables, JsonOptions);
    }

    private static MicroflowActionExecutionResult Failed(
        MicroflowActionExecutionContext context,
        string code,
        string message,
        string? details = null)
        => new()
        {
            Status = MicroflowActionExecutionStatus.Failed,
            OutputPreview = message,
            Error = new MicroflowRuntimeErrorDto
            {
                Code = code,
                Message = message,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId,
                Details = details
            },
            ShouldContinueNormalFlow = false,
            ShouldEnterErrorHandler = true,
            ShouldStopRun = true,
            Message = message
        };

    private static JsonElement ReadObject(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var value) ? value : default;

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object ? MicroflowSchemaReader.ReadString(element, propertyName) : null;

    private static bool ReadBool(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.True;

    private static string? ReadExpressionText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element.GetRawText();
        }

        return ReadString(element, "raw") ?? ReadString(element, "text") ?? ReadString(element, "expression");
    }

    [GeneratedRegex(@"\{(\d+)\}")]
    private static partial Regex PlaceholderRegex();
}
