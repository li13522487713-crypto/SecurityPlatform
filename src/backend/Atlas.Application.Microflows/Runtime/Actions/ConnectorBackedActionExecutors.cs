using System.Text.Json;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Runtime.Actions;

public sealed class SoapWebServiceActionExecutor : IMicroflowActionExecutor
{
    public string ActionKind => "webServiceCall";

    public string Category => MicroflowActionRuntimeCategory.ConnectorBacked;

    public string SupportLevel => MicroflowActionSupportLevel.RequiresConnector;

    public Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
        => ConnectorBackedExecutionHelper.ExecuteConnectorBackedAsync(
            context,
            ct,
            capability: MicroflowRuntimeConnectorCapability.SoapWebService,
            defaultReason: "SOAP/WSDL execution requires web service connector.");
}

public sealed class XmlMappingActionExecutor : IMicroflowActionExecutor
{
    public string ActionKind => "importXml";

    public string Category => MicroflowActionRuntimeCategory.ConnectorBacked;

    public string SupportLevel => MicroflowActionSupportLevel.RequiresConnector;

    public Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
    {
        var (capability, reason) = context.ActionKind switch
        {
            "exportXml" => (MicroflowRuntimeConnectorCapability.XmlExportMapping, "XML export mapping requires mapping connector."),
            _ => (MicroflowRuntimeConnectorCapability.XmlImportMapping, "XML import mapping requires mapping connector.")
        };

        return ConnectorBackedExecutionHelper.ExecuteConnectorBackedAsync(context, ct, capability, reason);
    }
}

public sealed class DocumentGenerationActionExecutor : IMicroflowActionExecutor
{
    public string ActionKind => "generateDocument";

    public string Category => MicroflowActionRuntimeCategory.ConnectorBacked;

    public string SupportLevel => MicroflowActionSupportLevel.Deprecated;

    public Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
        => ConnectorBackedExecutionHelper.ExecuteConnectorBackedAsync(
            context,
            ct,
            capability: MicroflowRuntimeConnectorCapability.DocumentGeneration,
            defaultReason: "Document generation is deprecated and requires document connector.");
}

public sealed class ExternalObjectActionExecutor : IMicroflowActionExecutor
{
    public string ActionKind => "createExternalObject";

    public string Category => MicroflowActionRuntimeCategory.ConnectorBacked;

    public string SupportLevel => MicroflowActionSupportLevel.RequiresConnector;

    public Task<MicroflowActionExecutionResult> ExecuteAsync(MicroflowActionExecutionContext context, CancellationToken ct)
        => ConnectorBackedExecutionHelper.ExecuteConnectorBackedAsync(
            context,
            ct,
            capability: MicroflowRuntimeConnectorCapability.ExternalObjectCrud,
            defaultReason: "External object CRUD connector required.");
}

internal static class ConnectorBackedExecutionHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<MicroflowActionExecutionResult> ExecuteConnectorBackedAsync(
        MicroflowActionExecutionContext context,
        CancellationToken ct,
        string capability,
        string defaultReason)
    {
        ct.ThrowIfCancellationRequested();
        var reason = ResolveReason(context.ActionKind, defaultReason);
        var request = new MicroflowConnectorExecutionRequest
        {
            Capability = capability,
            ActionKind = context.ActionKind,
            ObjectId = context.ObjectId,
            ActionId = context.ActionId,
            PayloadJson = context.ActionConfig.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? null : context.ActionConfig.GetRawText()
        };

        if (!context.ConnectorRegistry.HasCapability(capability))
        {
            return ConnectorRequired(context, request, reason);
        }

        if (!TryValidate(context.ActionKind, context.ActionConfig, out var validationMessage))
        {
            return ValidationFailed(context, request, validationMessage);
        }

        var connectorResult = await context.ConnectorRegistry.ExecuteAsync(request, ct).ConfigureAwait(false);
        if (!connectorResult.Success)
        {
            var error = connectorResult.Error ?? new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeConnectorRequired,
                Message = reason,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId
            };
            var code = string.IsNullOrWhiteSpace(error.Code) ? RuntimeErrorCode.RuntimeUnknownError : error.Code;
            var status = string.Equals(code, RuntimeErrorCode.RuntimeConnectorRequired, StringComparison.OrdinalIgnoreCase)
                ? MicroflowActionExecutionStatus.ConnectorRequired
                : MicroflowActionExecutionStatus.Failed;
            return new MicroflowActionExecutionResult
            {
                Status = status,
                Error = error with
                {
                    Code = code,
                    ObjectId = error.ObjectId ?? context.ObjectId,
                    ActionId = error.ActionId ?? context.ActionId
                },
                ConnectorRequests = [request],
                Logs = connectorResult.Logs,
                Diagnostics =
                [
                    new MicroflowActionExecutionDiagnostic
                    {
                        Code = code,
                        Severity = "error",
                        Message = error.Message ?? reason,
                        ActionKind = context.ActionKind,
                        ObjectId = context.ObjectId,
                        ActionId = context.ActionId,
                        ConnectorCapability = capability
                    }
                ],
                ShouldContinueNormalFlow = false,
                ShouldEnterErrorHandler = true,
                ShouldStopRun = true,
                Message = error.Message ?? reason
            };
        }

        return new MicroflowActionExecutionResult
        {
            Status = MicroflowActionExecutionStatus.Success,
            OutputJson = TryParseJson(connectorResult.OutputJson) ?? JsonSerializer.SerializeToElement(new
            {
                actionKind = context.ActionKind,
                connectorCapability = capability,
                outputPreview = reason
            }, JsonOptions),
            OutputPreview = reason,
            ConnectorRequests = [request],
            Logs = connectorResult.Logs,
            Message = reason
        };
    }

    private static MicroflowActionExecutionResult ConnectorRequired(
        MicroflowActionExecutionContext context,
        MicroflowConnectorExecutionRequest request,
        string reason)
        => new()
        {
            Status = MicroflowActionExecutionStatus.ConnectorRequired,
            Error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeConnectorRequired,
                Message = reason,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId,
                Details = JsonSerializer.Serialize(new { context.ActionKind, request.Capability }, JsonOptions)
            },
            ConnectorRequests = [request],
            Diagnostics =
            [
                new MicroflowActionExecutionDiagnostic
                {
                    Code = RuntimeErrorCode.RuntimeConnectorRequired,
                    Severity = "error",
                    Message = reason,
                    ActionKind = context.ActionKind,
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId,
                    ConnectorCapability = request.Capability
                }
            ],
            ShouldContinueNormalFlow = false,
            ShouldEnterErrorHandler = true,
            ShouldStopRun = true,
            Message = reason
        };

    private static MicroflowActionExecutionResult ValidationFailed(
        MicroflowActionExecutionContext context,
        MicroflowConnectorExecutionRequest request,
        string message)
        => new()
        {
            Status = MicroflowActionExecutionStatus.Failed,
            Error = new MicroflowRuntimeErrorDto
            {
                Code = RuntimeErrorCode.RuntimeValidationBlocked,
                Message = message,
                ObjectId = context.ObjectId,
                ActionId = context.ActionId
            },
            ConnectorRequests = [request],
            Diagnostics =
            [
                new MicroflowActionExecutionDiagnostic
                {
                    Code = RuntimeErrorCode.RuntimeValidationBlocked,
                    Severity = "error",
                    Message = message,
                    ActionKind = context.ActionKind,
                    ObjectId = context.ObjectId,
                    ActionId = context.ActionId,
                    ConnectorCapability = request.Capability
                }
            ],
            ShouldContinueNormalFlow = false,
            ShouldEnterErrorHandler = true,
            ShouldStopRun = true,
            Message = message
        };

    private static string ResolveReason(string actionKind, string fallback)
        => actionKind switch
        {
            "createExternalObject" => "External object create requires external object connector capability.",
            "changeExternalObject" => "External object update requires external object connector capability.",
            "sendExternalObject" => "External object send requires external object connector capability.",
            "deleteExternalObject" => "External object delete requires external object connector capability.",
            "externalObject" => "Legacy external object action requires connector.",
            _ => fallback
        };

    private static bool TryValidate(string actionKind, JsonElement actionConfig, out string message)
    {
        message = string.Empty;
        if (actionKind is "importXml" or "exportXml")
        {
            var hasPayload = HasNonEmptyString(actionConfig, "xml")
                || HasNonEmptyString(actionConfig, "sourceXml")
                || HasObject(actionConfig, "mapping");
            if (!hasPayload)
            {
                message = $"{actionKind} requires xml/sourceXml or mapping payload.";
                return false;
            }
        }

        if (actionKind is "createExternalObject" or "changeExternalObject" or "sendExternalObject" or "deleteExternalObject" or "externalObject")
        {
            var hasTarget = HasNonEmptyString(actionConfig, "externalObjectType")
                || HasNonEmptyString(actionConfig, "targetType")
                || HasNonEmptyString(actionConfig, "entity")
                || HasNonEmptyString(actionConfig, "connectorObjectType");
            if (!hasTarget)
            {
                message = $"{actionKind} requires externalObjectType/targetType/entity.";
                return false;
            }
        }

        if (actionKind == "generateDocument")
        {
            var hasTemplate = HasNonEmptyString(actionConfig, "templateId")
                || HasNonEmptyString(actionConfig, "templateName")
                || HasNonEmptyString(actionConfig, "documentDefinition");
            if (!hasTemplate)
            {
                message = "generateDocument requires templateId/templateName/documentDefinition.";
                return false;
            }
        }

        return true;
    }

    private static bool HasNonEmptyString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(property, out var value)
           && value.ValueKind == JsonValueKind.String
           && !string.IsNullOrWhiteSpace(value.GetString());

    private static bool HasObject(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(property, out var value)
           && value.ValueKind == JsonValueKind.Object;

    private static JsonElement? TryParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new { rawOutput = json }, JsonOptions);
        }
    }
}
