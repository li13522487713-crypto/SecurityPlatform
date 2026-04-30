using System.Globalization;
using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Expressions;

namespace Atlas.Application.Microflows.Runtime.Actions;

public sealed class MetricsActionExecutor : IMicroflowActionExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string ActionKind => "metrics";

    public string Category => MicroflowActionRuntimeCategory.ServerExecutable;

    public string SupportLevel => MicroflowActionSupportLevel.Supported;

    public Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var metricKind = context.ActionKind;
        var metricName = ReadString(context.ActionConfig, "metricName");
        if (string.IsNullOrWhiteSpace(metricName))
        {
            return Task.FromResult(Failed(context, RuntimeErrorCode.RuntimeExpressionError, $"{metricKind} requires metricName."));
        }

        decimal metricValue;
        if (string.Equals(metricKind, "incrementCounter", StringComparison.OrdinalIgnoreCase))
        {
            metricValue = 1m;
        }
        else
        {
            if (context.ExpressionEvaluator is null)
            {
                return Task.FromResult(Failed(context, RuntimeErrorCode.RuntimeExpressionError, $"{metricKind} requires ExpressionEvaluator."));
            }

            var expressionRaw = ReadExpressionRaw(context.ActionConfig, "valueExpression");
            if (string.IsNullOrWhiteSpace(expressionRaw))
            {
                return Task.FromResult(Failed(context, RuntimeErrorCode.RuntimeExpressionError, $"{metricKind} requires valueExpression."));
            }

            var evaluation = context.ExpressionEvaluator.Evaluate(
                expressionRaw!,
                new MicroflowExpressionEvaluationContext
                {
                    RuntimeExecutionContext = context.RuntimeExecutionContext,
                    VariableStore = context.VariableStore,
                    MetadataCatalog = context.MetadataCatalog,
                    MetadataResolver = context.MetadataResolver,
                    CurrentObjectId = context.ObjectId,
                    CurrentActionId = context.ActionId,
                    CurrentCollectionId = context.CollectionId,
                    ExpectedType = MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.Decimal),
                    Mode = context.Options.Mode,
                    Options = new MicroflowExpressionEvaluationOptions
                    {
                        AllowUnknownVariables = false,
                        AllowUnsupportedFunctions = false,
                        StrictTypeCheck = true
                    }
                });
            if (!evaluation.Success || string.IsNullOrWhiteSpace(evaluation.RawValueJson) || !TryReadNumeric(evaluation.RawValueJson!, out metricValue))
            {
                return Task.FromResult(Failed(
                    context,
                    evaluation.Error?.Code ?? RuntimeErrorCode.RuntimeExpressionError,
                    evaluation.Error?.Message ?? $"{metricKind} valueExpression must evaluate to a numeric value.",
                    evaluation.Error?.Details));
            }
        }

        var tags = ReadTags(context.ActionConfig);
        var valuePreview = metricValue.ToString(CultureInfo.InvariantCulture);
        var log = new MicroflowRuntimeLogDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow,
            Level = "info",
            ObjectId = context.ObjectId,
            ActionId = context.ActionId,
            LogNodeName = metricKind,
            TraceId = context.RuntimeExecutionContext.RunId,
            Message = $"{metricKind}:{metricName}={valuePreview}",
            StructuredFieldsJson = JsonSerializer.Serialize(new
            {
                kind = "metrics",
                metricKind,
                metricName,
                value = valuePreview,
                tags
            }, JsonOptions)
        };

        var output = JsonSerializer.SerializeToElement(new
        {
            metrics = new
            {
                metricKind,
                metricName,
                valuePreview,
                tags
            }
        }, JsonOptions);

        return Task.FromResult(new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = output,
            OutputPreview = $"{metricName}={valuePreview}",
            Logs = [log],
            ShouldContinueNormalFlow = true,
            Message = $"{metricKind} emitted."
        });
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    private static string? ReadExpressionRaw(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return value.ValueKind == JsonValueKind.Object
               && value.TryGetProperty("raw", out var raw)
               && raw.ValueKind == JsonValueKind.String
            ? raw.GetString()
            : null;
    }

    private static IReadOnlyList<string> ReadTags(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return tags.EnumerateArray()
            .Where(tag => tag.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(tag.GetString()))
            .Select(tag => tag.GetString()!)
            .ToArray();
    }

    private static bool TryReadNumeric(string rawValueJson, out decimal value)
    {
        var element = MicroflowVariableStore.ToJsonElement(rawValueJson);
        if (element.HasValue && element.Value.ValueKind == JsonValueKind.Number)
        {
            value = element.Value.GetDecimal();
            return true;
        }

        if (element.HasValue
            && element.Value.ValueKind == JsonValueKind.String
            && decimal.TryParse(element.Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static MicroflowActionExecutionResult Failed(MicroflowActionExecutionContext context, string code, string message, string? details = null)
        => new()
        {
            Status = MicroflowActionExecutionStatus.Failed,
            Error = new MicroflowRuntimeErrorDto
            {
                Code = code,
                Message = message,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId,
                Details = details
            },
            OutputPreview = message,
            ShouldContinueNormalFlow = false,
            ShouldStopRun = true,
            Message = message
        };
}
