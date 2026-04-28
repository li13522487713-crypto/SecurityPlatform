using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Expressions;
using Atlas.Application.Microflows.Services;

namespace Atlas.Application.Microflows.Runtime.Actions.Http;

public sealed class MicroflowRestRequestBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET",
        "POST",
        "PUT",
        "PATCH",
        "DELETE"
    };

    public async Task<MicroflowRestRequestBuildResult> BuildAsync(
        MicroflowActionExecutionContext context,
        MicroflowRuntimeHttpOptions options,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (context.ExpressionEvaluator is null)
        {
            return Failed(context, RuntimeErrorCode.RuntimeExpressionError, "RestCall requires ExpressionEvaluator.");
        }

        var action = context.ActionConfig;
        if (!action.TryGetProperty("request", out var requestConfig))
        {
            return Failed(context, RuntimeErrorCode.RuntimeRestInvalidUrl, "RestCall request config is required.");
        }

        var method = (ReadString(requestConfig, "method") ?? "GET").Trim().ToUpperInvariant();
        if (!AllowedMethods.Contains(method))
        {
            return Failed(context, RuntimeErrorCode.RuntimeRestCallFailed, $"Unsupported REST method '{method}'.");
        }

        var urlExpression = ReadExpressionText(requestConfig, "urlExpression");
        if (string.IsNullOrWhiteSpace(urlExpression))
        {
            return Failed(context, RuntimeErrorCode.RuntimeRestInvalidUrl, "RestCall request.urlExpression is required.");
        }

        var urlResult = Evaluate(context, urlExpression!, MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.String));
        if (!urlResult.Success)
        {
            return Failed(context, RuntimeErrorCode.RuntimeExpressionError, urlResult.Error?.Message ?? "RestCall URL expression failed.", urlResult);
        }

        var url = urlResult.Value?.StringValue ?? urlResult.ValuePreview;
        if (string.IsNullOrWhiteSpace(url))
        {
            return Failed(context, RuntimeErrorCode.RuntimeRestInvalidUrl, "RestCall URL evaluated to empty string.");
        }

        Dictionary<string, string> headers;
        Dictionary<string, string> query;
        IReadOnlyDictionary<string, string> queryPreview;
        BodyBuildResult bodyResult;
        try
        {
            headers = EvaluateKeyValues(context, requestConfig, "headers", options, redact: false, out _);
            query = EvaluateKeyValues(context, requestConfig, "queryParameters", options, redact: false, out queryPreview);
            bodyResult = await BuildBodyAsync(context, requestConfig, headers, options, ct);
        }
        catch (MicroflowRestBuildException ex)
        {
            return new MicroflowRestRequestBuildResult { Success = false, Error = ex.Error };
        }

        var urlWithQuery = MergeQuery(url!, query);
        if (!bodyResult.Success)
        {
            return bodyResult;
        }

        var finalHeaders = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
        EnsureDefaultContentType(finalHeaders, bodyResult.BodyKind);
        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(bodyResult.BodyKind, MicroflowRestBodyKind.None, StringComparison.OrdinalIgnoreCase))
        {
            bodyResult.Diagnostics.Add(new MicroflowActionExecutionDiagnostic
            {
                Code = RuntimeErrorCode.RuntimeRestCallFailed,
                Severity = "warning",
                Message = "GET body is ignored by Microflow Runtime.",
                ActionKind = context.ActionKind,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId
            });
        }

        var request = new MicroflowRuntimeHttpRequest
        {
            Method = method,
            Url = urlWithQuery,
            Headers = finalHeaders,
            QueryParameters = query,
            BodyKind = string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ? MicroflowRestBodyKind.None : bodyResult.BodyKind,
            BodyText = bodyResult.BodyText,
            BodyJson = bodyResult.BodyJson,
            FormFields = bodyResult.FormFields,
            TimeoutSeconds = ReadInt(context.ActionConfig, "timeoutSeconds"),
            TraceId = context.RuntimeExecutionContext.RunId,
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId
        };

        return new MicroflowRestRequestBuildResult
        {
            Success = true,
            Request = request,
            RequestPreview = new MicroflowRestRequestPreview
            {
                Method = method,
                Url = urlWithQuery,
                Headers = MicroflowRestRedaction.RedactHeaders(finalHeaders, options.RedactHeaders),
                Query = queryPreview,
                BodyKind = request.BodyKind,
                BodyPreview = MicroflowVariableStore.TrimPreview(bodyResult.BodyPreview, 300),
                ExternalRequestSent = options.AllowRealHttp
            },
            Diagnostics = bodyResult.Diagnostics
        };
    }

    private async Task<BodyBuildResult> BuildBodyAsync(
        MicroflowActionExecutionContext context,
        JsonElement requestConfig,
        IReadOnlyDictionary<string, string> headers,
        MicroflowRuntimeHttpOptions options,
        CancellationToken ct)
    {
        if (!requestConfig.TryGetProperty("body", out var bodyConfig) || bodyConfig.ValueKind != JsonValueKind.Object)
        {
            return BodyBuildResult.None();
        }

        var kind = (ReadString(bodyConfig, "kind") ?? ReadString(bodyConfig, "type") ?? MicroflowRestBodyKind.None).Trim();
        switch (kind)
        {
            case MicroflowRestBodyKind.None:
                return BodyBuildResult.None();
            case MicroflowRestBodyKind.Json:
            {
                var expression = ReadExpressionText(bodyConfig, "expression");
                if (string.IsNullOrWhiteSpace(expression))
                {
                return BodyBuildResult.Ok(MicroflowRestBodyKind.Json, "null", null, "null");
                }

                var evaluated = Evaluate(context, expression!, expectedType: null);
                if (!evaluated.Success)
                {
                    return BodyBuildResult.Failed(context, RuntimeErrorCode.RuntimeExpressionError, evaluated.Error?.Message ?? "RestCall JSON body expression failed.", evaluated);
                }

                var bodyText = evaluated.RawValueJson ?? JsonSerializer.Serialize(evaluated.ValuePreview, JsonOptions);
                var bodyJson = TryParseJson(bodyText);
                return BodyBuildResult.Ok(MicroflowRestBodyKind.Json, bodyText, bodyJson, evaluated.ValuePreview);
            }
            case MicroflowRestBodyKind.Text:
            {
                var expression = ReadExpressionText(bodyConfig, "expression");
                var evaluated = string.IsNullOrWhiteSpace(expression)
                    ? null
                    : Evaluate(context, expression!, MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.String));
                if (evaluated is { Success: false })
                {
                    return BodyBuildResult.Failed(context, RuntimeErrorCode.RuntimeExpressionError, evaluated.Error?.Message ?? "RestCall text body expression failed.", evaluated);
                }

                var bodyText = evaluated?.Value?.StringValue ?? evaluated?.ValuePreview ?? string.Empty;
                return BodyBuildResult.Ok(MicroflowRestBodyKind.Text, bodyText, null, bodyText);
            }
            case MicroflowRestBodyKind.Form:
            {
                var fields = EvaluateKeyValues(context, bodyConfig, "fields", options, redact: false, out _);
                return BodyBuildResult.Ok(MicroflowRestBodyKind.Form, null, null, string.Join("&", fields.Select(pair => $"{pair.Key}=...")), fields);
            }
            case MicroflowRestBodyKind.Mapping:
            {
                const string capability = MicroflowRuntimeConnectorCapability.RestExportMapping;
                if (!context.ConnectorRegistry.HasCapability(capability))
                {
                    return BodyBuildResult.Failed(context, RuntimeErrorCode.RuntimeConnectorRequired, "REST body mapping requires export mapping connector.", connectorCapability: capability);
                }

                var connectorResult = await context.ConnectorRegistry.ExecuteAsync(new MicroflowConnectorExecutionRequest
                {
                    Capability = capability,
                    ActionKind = context.ActionKind,
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId,
                    PayloadJson = bodyConfig.GetRawText()
                }, ct);
                if (!connectorResult.Success)
                {
                    return BodyBuildResult.Failed(context, connectorResult.Error?.Code ?? RuntimeErrorCode.RuntimeConnectorRequired, connectorResult.Error?.Message ?? "REST body mapping connector failed.", connectorCapability: capability);
                }

                return BodyBuildResult.Ok(MicroflowRestBodyKind.Mapping, connectorResult.OutputJson ?? string.Empty, TryParseJson(connectorResult.OutputJson), "mapping body");
            }
            default:
                return BodyBuildResult.Failed(context, RuntimeErrorCode.RuntimeRestCallFailed, $"Unsupported REST body kind '{kind}'.");
        }
    }

    private static Dictionary<string, string> EvaluateKeyValues(
        MicroflowActionExecutionContext context,
        JsonElement owner,
        string collectionName,
        MicroflowRuntimeHttpOptions options,
        bool redact,
        out IReadOnlyDictionary<string, string> preview)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var previewValues = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!owner.TryGetProperty(collectionName, out var collection) || collection.ValueKind != JsonValueKind.Array)
        {
            preview = previewValues;
            return values;
        }

        foreach (var item in collection.EnumerateArray())
        {
            var key = ReadString(item, "key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var expression = ReadExpressionText(item, "valueExpression") ?? ReadExpressionText(item, "value");
            var value = string.Empty;
            if (!string.IsNullOrWhiteSpace(expression))
            {
                var evaluated = Evaluate(context, expression!, MicroflowExpressionType.Simple(MicroflowExpressionTypeKind.String));
                if (!evaluated.Success)
                {
                    throw new MicroflowRestBuildException(new MicroflowRuntimeErrorDto
                    {
                        Code = RuntimeErrorCode.RuntimeExpressionError,
                        Message = evaluated.Error?.Message ?? $"RestCall {collectionName} expression failed.",
                        ObjectId = context.ObjectId,
                        ActionId = context.ActionId,
                        Details = JsonSerializer.Serialize(evaluated.Diagnostics, JsonOptions)
                    });
                }

                value = evaluated.Value?.StringValue ?? evaluated.ValuePreview;
            }

            values[key!] = value;
            previewValues[key!] = redact ? MicroflowRestRedaction.RedactHeaderValue(key!, value, options.RedactHeaders) : value;
        }

        preview = previewValues;
        return values;
    }

    private static MicroflowExpressionEvaluationResult Evaluate(
        MicroflowActionExecutionContext context,
        string expression,
        MicroflowExpressionType? expectedType)
        => context.ExpressionEvaluator!.Evaluate(
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
                ExpectedType = expectedType,
                Mode = context.Options.Mode,
                Options = new MicroflowExpressionEvaluationOptions
                {
                    AllowUnknownVariables = false,
                    AllowUnsupportedFunctions = false,
                    CoerceNumericTypes = true,
                    StrictTypeCheck = true,
                    MaxStringLength = 500,
                    MaxEvaluationDepth = 64
                }
            });

    private static string MergeQuery(string url, IReadOnlyDictionary<string, string> query)
    {
        if (query.Count == 0)
        {
            return url;
        }

        var builder = new UriBuilder(url);
        var existing = string.IsNullOrWhiteSpace(builder.Query) ? string.Empty : builder.Query.TrimStart('?') + "&";
        builder.Query = existing + string.Join("&", query.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return builder.Uri.AbsoluteUri;
    }

    private static void EnsureDefaultContentType(Dictionary<string, string> headers, string bodyKind)
    {
        if (headers.Keys.Any(key => string.Equals(key, "content-type", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (string.Equals(bodyKind, MicroflowRestBodyKind.Json, StringComparison.OrdinalIgnoreCase)
            || string.Equals(bodyKind, MicroflowRestBodyKind.Mapping, StringComparison.OrdinalIgnoreCase))
        {
            headers["content-type"] = "application/json";
        }
        else if (string.Equals(bodyKind, MicroflowRestBodyKind.Text, StringComparison.OrdinalIgnoreCase))
        {
            headers["content-type"] = "text/plain";
        }
        else if (string.Equals(bodyKind, MicroflowRestBodyKind.Form, StringComparison.OrdinalIgnoreCase))
        {
            headers["content-type"] = "application/x-www-form-urlencoded";
        }
    }

    private static MicroflowRestRequestBuildResult Failed(
        MicroflowActionExecutionContext context,
        string code,
        string message,
        MicroflowExpressionEvaluationResult? expressionResult = null)
        => new()
        {
            Success = false,
            Error = new MicroflowRuntimeErrorDto
            {
                Code = code,
                Message = message,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId,
                Details = expressionResult is null ? null : JsonSerializer.Serialize(expressionResult.Diagnostics, JsonOptions)
            }
        };

    private static string? ReadString(JsonElement element, string propertyName)
        => MicroflowSchemaReader.ReadString(element, propertyName);

    private static int? ReadInt(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var result)
                ? result
                : null;

    private static string? ReadExpressionText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
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

    private static JsonElement? TryParseJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record BodyBuildResult
    {
        public bool Success { get; init; }
        public string BodyKind { get; init; } = MicroflowRestBodyKind.None;
        public string? BodyText { get; init; }
        public JsonElement? BodyJson { get; init; }
        public string? BodyPreview { get; init; }
        public Dictionary<string, string> FormFields { get; init; } = new(StringComparer.Ordinal);
        public List<MicroflowActionExecutionDiagnostic> Diagnostics { get; init; } = [];
        public MicroflowRestRequestBuildResult? Failure { get; init; }

        public static BodyBuildResult None() => new() { Success = true };

        public static BodyBuildResult Ok(string kind, string? text, JsonElement? json, string? preview, Dictionary<string, string>? fields = null)
            => new() { Success = true, BodyKind = kind, BodyText = text, BodyJson = json, BodyPreview = preview, FormFields = fields ?? new Dictionary<string, string>(StringComparer.Ordinal) };

        public static BodyBuildResult Failed(MicroflowActionExecutionContext context, string code, string message, MicroflowExpressionEvaluationResult? expression = null, string? connectorCapability = null)
            => new()
            {
                Success = false,
                Failure = new MicroflowRestRequestBuildResult
                {
                    Success = false,
                    Error = new MicroflowRuntimeErrorDto
                    {
                        Code = code,
                        Message = message,
                        ObjectId = context.ObjectId,
                        ActionId = context.ActionId,
                        Details = expression is null
                            ? connectorCapability is null ? null : JsonSerializer.Serialize(new { connectorCapability }, JsonOptions)
                            : JsonSerializer.Serialize(expression.Diagnostics, JsonOptions)
                    },
                    Diagnostics = connectorCapability is null
                        ? Array.Empty<MicroflowActionExecutionDiagnostic>()
                        :
                        [
                            new MicroflowActionExecutionDiagnostic
                            {
                                Code = code,
                                Severity = "error",
                                Message = message,
                                ActionKind = context.ActionKind,
                                ObjectId = context.ObjectId,
                                ActionId = context.ActionId,
                                ConnectorCapability = connectorCapability
                            }
                        ]
                }
            };

        public static implicit operator MicroflowRestRequestBuildResult(BodyBuildResult result)
            => result.Failure ?? new MicroflowRestRequestBuildResult { Success = result.Success };
    }

    private sealed class MicroflowRestBuildException : Exception
    {
        public MicroflowRestBuildException(MicroflowRuntimeErrorDto error)
            : base(error.Message)
        {
            Error = error;
        }

        public MicroflowRuntimeErrorDto Error { get; }
    }
}
