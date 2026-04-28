using System.Text.Json;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Services;

namespace Atlas.Application.Microflows.Runtime.Actions.Http;

public sealed class MicroflowRestResponseHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MicroflowRestResponseHandleResult> HandleAsync(
        MicroflowActionExecutionContext context,
        MicroflowRuntimeHttpRequest request,
        MicroflowRuntimeHttpResponse response,
        MicroflowRuntimeHttpOptions options,
        CancellationToken ct)
    {
        var produced = new List<MicroflowRuntimeVariableValueDto>();
        var diagnostics = new List<MicroflowActionExecutionDiagnostic>();
        var responseConfig = context.ActionConfig.TryGetProperty("response", out var responseElement)
            ? responseElement
            : default;
        var handlingConfig = responseConfig.ValueKind == JsonValueKind.Object && responseConfig.TryGetProperty("handling", out var handling)
            ? handling
            : default;
        var handlingKind = ReadString(handlingConfig, "kind")
            ?? ReadString(handlingConfig, "type")
            ?? ReadString(responseConfig, "handling")
            ?? MicroflowRestResponseHandlingKind.Ignore;

        var redactedHeaders = MicroflowRestRedaction.RedactHeaders(response.Headers, options.RedactHeaders);
        var statusCodeVariableName = ReadString(responseConfig, "statusCodeVariableName")
            ?? ReadString(context.ActionConfig, "statusCodeVariableName");
        if (!string.IsNullOrWhiteSpace(statusCodeVariableName))
        {
            produced.Add(UpsertVariable(
                context,
                statusCodeVariableName!,
                JsonSerializer.Serialize(new { kind = "integer" }, JsonOptions),
                JsonSerializer.Serialize(response.StatusCode, JsonOptions),
                response.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"));
        }

        var headersVariableName = ReadString(responseConfig, "headersVariableName")
            ?? ReadString(context.ActionConfig, "headersVariableName");
        if (!string.IsNullOrWhiteSpace(headersVariableName))
        {
            var headersJson = JsonSerializer.Serialize(redactedHeaders, JsonOptions);
            produced.Add(UpsertVariable(
                context,
                headersVariableName!,
                JsonSerializer.Serialize(new { kind = "json" }, JsonOptions),
                headersJson,
                MicroflowVariableStore.TrimPreview(headersJson, 200)));
        }

        var outputVariableName = ReadString(handlingConfig, "outputVariableName")
            ?? ReadString(responseConfig, "outputVariableName")
            ?? ReadString(context.ActionConfig, "outputVariableName");
        switch (handlingKind)
        {
            case MicroflowRestResponseHandlingKind.Ignore:
                break;
            case MicroflowRestResponseHandlingKind.String:
                if (string.IsNullOrWhiteSpace(outputVariableName))
                {
                    return Failed(context, RuntimeErrorCode.RuntimeRestCallFailed, "RestCall string response handling requires outputVariableName.", produced, diagnostics);
                }

                produced.Add(UpsertVariable(
                    context,
                    outputVariableName!,
                    JsonSerializer.Serialize(new { kind = "string" }, JsonOptions),
                    JsonSerializer.Serialize(response.BodyText ?? string.Empty, JsonOptions),
                    response.BodyPreview ?? string.Empty));
                break;
            case MicroflowRestResponseHandlingKind.Json:
                if (string.IsNullOrWhiteSpace(outputVariableName))
                {
                    return Failed(context, RuntimeErrorCode.RuntimeRestCallFailed, "RestCall JSON response handling requires outputVariableName.", produced, diagnostics);
                }

                if (!response.BodyJson.HasValue)
                {
                    return Failed(context, RuntimeErrorCode.RuntimeRestResponseParseFailed, "RestCall response body is not valid JSON.", produced, diagnostics);
                }

                produced.Add(UpsertVariable(
                    context,
                    outputVariableName!,
                    JsonSerializer.Serialize(new { kind = "json" }, JsonOptions),
                    response.BodyJson.Value.GetRawText(),
                    response.BodyPreview ?? response.BodyJson.Value.GetRawText()));
                break;
            case MicroflowRestResponseHandlingKind.ImportMapping:
            {
                if (string.IsNullOrWhiteSpace(outputVariableName))
                {
                    return Failed(context, RuntimeErrorCode.RuntimeRestCallFailed, "RestCall importMapping response handling requires outputVariableName.", produced, diagnostics);
                }

                const string capability = MicroflowRuntimeConnectorCapability.RestImportMapping;
                if (!context.ConnectorRegistry.HasCapability(capability))
                {
                    diagnostics.Add(new MicroflowActionExecutionDiagnostic
                    {
                        Code = RuntimeErrorCode.RuntimeConnectorRequired,
                        Severity = "error",
                        Message = "REST import mapping requires mapping connector.",
                        ActionKind = context.ActionKind,
                        ObjectId = context.ObjectId,
                        ActionId = context.ActionId,
                        ConnectorCapability = capability
                    });
                    return Failed(context, RuntimeErrorCode.RuntimeConnectorRequired, "REST import mapping requires mapping connector.", produced, diagnostics, capability);
                }

                var connectorResult = await context.ConnectorRegistry.ExecuteAsync(new MicroflowConnectorExecutionRequest
                {
                    Capability = capability,
                    ActionKind = context.ActionKind,
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId,
                    PayloadJson = JsonSerializer.Serialize(new { response.BodyText, response.BodyJson }, JsonOptions)
                }, ct);
                if (!connectorResult.Success)
                {
                    return Failed(context, connectorResult.Error?.Code ?? RuntimeErrorCode.RuntimeConnectorRequired, connectorResult.Error?.Message ?? "REST import mapping connector failed.", produced, diagnostics, capability);
                }

                produced.Add(UpsertVariable(
                    context,
                    outputVariableName!,
                    JsonSerializer.Serialize(new { kind = "object" }, JsonOptions),
                    connectorResult.OutputJson ?? "null",
                    MicroflowVariableStore.Preview(connectorResult.OutputJson)));
                break;
            }
            default:
                return Failed(context, RuntimeErrorCode.RuntimeRestCallFailed, $"Unsupported response handling '{handlingKind}'.", produced, diagnostics);
        }

        var output = JsonSerializer.SerializeToElement(new
        {
            restCall = new
            {
                request.Method,
                request.Url,
                statusCode = response.StatusCode,
                response.ReasonPhrase,
                responseHandling = handlingKind,
                responsePreview = response.BodyPreview,
                headers = redactedHeaders,
                producedVariables = produced.Select(variable => variable.Name).ToArray(),
                response.DurationMs,
                response.Truncated
            }
        }, JsonOptions);

        return new MicroflowRestResponseHandleResult
        {
            Success = true,
            OutputJson = output,
            OutputPreview = $"HTTP {response.StatusCode?.ToString() ?? "n/a"} {response.ReasonPhrase}".Trim(),
            ProducedVariables = produced,
            Diagnostics = diagnostics
        };
    }

    private static MicroflowRuntimeVariableValueDto UpsertVariable(
        MicroflowActionExecutionContext context,
        string name,
        string dataTypeJson,
        string rawValueJson,
        string valuePreview)
    {
        var value = new MicroflowRuntimeVariableValue
        {
            Name = name,
            DataTypeJson = dataTypeJson,
            Kind = MicroflowVariableStore.InferKind(dataTypeJson, rawValueJson),
            RawValueJson = rawValueJson,
            ValuePreview = MicroflowVariableStore.TrimPreview(valuePreview, 200),
            TypePreview = MicroflowVariableStore.CreateTypePreview(dataTypeJson),
            SourceKind = MicroflowVariableSourceKind.RestResponse,
            SourceObjectId = context.ObjectId,
            SourceActionId = context.ActionId,
            CollectionId = context.CollectionId,
            ScopeKind = MicroflowVariableScopeKind.Action,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (context.VariableStore.Exists(name))
        {
            context.VariableStore.Set(name, value);
        }
        else
        {
            context.VariableStore.Define(new MicroflowVariableDefinition
            {
                Name = name,
                DataTypeJson = dataTypeJson,
                RawValueJson = rawValueJson,
                ValuePreview = valuePreview,
                SourceKind = MicroflowVariableSourceKind.RestResponse,
                SourceObjectId = context.ObjectId,
                SourceActionId = context.ActionId,
                CollectionId = context.CollectionId,
                ScopeKind = MicroflowVariableScopeKind.Action
            });
            value = context.VariableStore.Get(name);
        }

        return ToDto(value);
    }

    public static MicroflowRuntimeVariableValueDto ToDto(MicroflowRuntimeVariableValue value)
        => new()
        {
            Name = value.Name,
            Type = MicroflowVariableStore.ToJsonElement(value.DataTypeJson),
            ValuePreview = value.ValuePreview,
            RawValue = MicroflowVariableStore.ToJsonElement(value.RawValueJson),
            RawValueJson = value.RawValueJson,
            Source = value.SourceKind,
            Readonly = value.Readonly,
            ScopeKind = value.ScopeKind
        };

    private static MicroflowRestResponseHandleResult Failed(
        MicroflowActionExecutionContext context,
        string code,
        string message,
        IReadOnlyList<MicroflowRuntimeVariableValueDto> produced,
        IReadOnlyList<MicroflowActionExecutionDiagnostic> diagnostics,
        string? connectorCapability = null)
        => new()
        {
            Success = false,
            ProducedVariables = produced,
            Diagnostics = diagnostics,
            Error = new MicroflowRuntimeErrorDto
            {
                Code = code,
                Message = message,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId,
                Details = connectorCapability is null ? null : JsonSerializer.Serialize(new { connectorCapability }, JsonOptions)
            },
            OutputJson = JsonSerializer.SerializeToElement(new { restCall = new { failed = true, code, message } }, JsonOptions),
            OutputPreview = message
        };

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object ? MicroflowSchemaReader.ReadString(element, propertyName) : null;
}
