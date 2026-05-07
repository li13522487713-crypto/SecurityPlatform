using System.Collections.Concurrent;
using System.Text.Json;
using System.Xml.Linq;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Runtime.Actions;
using Atlas.Application.Microflows.Runtime.Connectors;

namespace Atlas.Infrastructure.Services.Microflows;

public sealed class DefaultSoapWebServiceConnector : ISoapWebServiceConnector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MicroflowConnectorCapabilityStatus GetCapabilityStatus()
        => new(MicroflowRuntimeConnectorCapability.SoapWebService, Available: true, "available");

    public Task<MicroflowConnectorExecutionResult> ExecuteAsync(
        MicroflowConnectorExecutionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = ParsePayload(request.PayloadJson);
        var endpoint = ReadString(payload, "endpoint", "url", "wsdlUrl");
        var operation = ReadString(payload, "operation", "action", "operationName");
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return Task.FromResult(Failed(request, RuntimeErrorCode.RuntimeValidationBlocked, "webServiceCall requires endpoint/url/wsdlUrl."));
        }

        var output = JsonSerializer.Serialize(new
        {
            kind = request.ActionKind,
            endpoint,
            operation = operation ?? "invoke",
            status = "simulated",
            note = "Default SOAP connector does not call remote network."
        }, JsonOptions);

        return Task.FromResult(Success(request.Capability, output));
    }

    private static MicroflowConnectorExecutionResult Success(string capability, string outputJson)
        => new()
        {
            Success = true,
            Capability = capability,
            OutputJson = outputJson
        };

    private static MicroflowConnectorExecutionResult Failed(MicroflowConnectorExecutionRequest request, string code, string message)
        => new()
        {
            Success = false,
            Capability = request.Capability,
            Error = new MicroflowRuntimeErrorDto
            {
                Code = code,
                Message = message,
                ObjectId = request.ObjectId,
                ActionId = request.ActionId
            }
        };

    private static Dictionary<string, JsonElement> ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            }

            var payload = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                payload[property.Name] = property.Value.Clone();
            }
            return payload;
        }
        catch
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? ReadString(Dictionary<string, JsonElement> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (payload.TryGetValue(key, out var value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString();
            }
        }

        return null;
    }
}

public sealed class DefaultXmlMappingConnector : IXmlMappingConnector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MicroflowConnectorCapabilityStatus GetCapabilityStatus()
        => new("xml.mapping", Available: true, "available");

    public Task<MicroflowConnectorExecutionResult> ExecuteAsync(
        MicroflowConnectorExecutionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = ParsePayload(request.PayloadJson);
        return request.ActionKind switch
        {
            "importXml" => Task.FromResult(HandleImport(request, payload)),
            "exportXml" => Task.FromResult(HandleExport(request, payload)),
            _ => Task.FromResult(Failed(request, RuntimeErrorCode.RuntimeValidationBlocked, $"Unsupported XML action: {request.ActionKind}"))
        };
    }

    private static MicroflowConnectorExecutionResult HandleImport(
        MicroflowConnectorExecutionRequest request,
        Dictionary<string, JsonElement> payload)
    {
        var xml = ReadString(payload, "sourceXml", "xml");
        if (string.IsNullOrWhiteSpace(xml))
        {
            return Failed(request, RuntimeErrorCode.RuntimeValidationBlocked, "importXml requires sourceXml/xml.");
        }

        try
        {
            var document = XDocument.Parse(xml);
            var output = JsonSerializer.Serialize(new
            {
                kind = "importXml",
                root = document.Root?.Name.LocalName,
                length = xml.Length
            }, JsonOptions);
            return Success(MicroflowRuntimeConnectorCapability.XmlImportMapping, output);
        }
        catch (Exception ex)
        {
            return Failed(request, RuntimeErrorCode.RuntimeValidationBlocked, $"Invalid XML: {ex.Message}");
        }
    }

    private static MicroflowConnectorExecutionResult HandleExport(
        MicroflowConnectorExecutionRequest request,
        Dictionary<string, JsonElement> payload)
    {
        var xml = ReadString(payload, "xml", "sourceXml");
        if (string.IsNullOrWhiteSpace(xml))
        {
            var rootName = ReadString(payload, "rootName") ?? "root";
            xml = new XElement(rootName).ToString(SaveOptions.DisableFormatting);
        }
        else
        {
            try
            {
                _ = XDocument.Parse(xml);
            }
            catch (Exception ex)
            {
                return Failed(request, RuntimeErrorCode.RuntimeValidationBlocked, $"Invalid XML: {ex.Message}");
            }
        }

        var output = JsonSerializer.Serialize(new
        {
            kind = "exportXml",
            xml,
            length = xml.Length
        }, JsonOptions);
        return Success(MicroflowRuntimeConnectorCapability.XmlExportMapping, output);
    }

    private static MicroflowConnectorExecutionResult Success(string capability, string outputJson)
        => new()
        {
            Success = true,
            Capability = capability,
            OutputJson = outputJson
        };

    private static MicroflowConnectorExecutionResult Failed(MicroflowConnectorExecutionRequest request, string code, string message)
        => new()
        {
            Success = false,
            Capability = request.Capability,
            Error = new MicroflowRuntimeErrorDto
            {
                Code = code,
                Message = message,
                ObjectId = request.ObjectId,
                ActionId = request.ActionId
            }
        };

    private static Dictionary<string, JsonElement> ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            }

            var payload = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                payload[property.Name] = property.Value.Clone();
            }
            return payload;
        }
        catch
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? ReadString(Dictionary<string, JsonElement> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (payload.TryGetValue(key, out var value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString();
            }
        }

        return null;
    }
}

public sealed class DefaultDocumentGenerationRuntime : IDocumentGenerationRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MicroflowConnectorCapabilityStatus GetCapabilityStatus()
        => new(MicroflowRuntimeConnectorCapability.DocumentGeneration, Available: true, "available");

    public Task<MicroflowConnectorExecutionResult> ExecuteAsync(
        MicroflowConnectorExecutionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = ParsePayload(request.PayloadJson);
        var templateId = ReadString(payload, "templateId", "templateName", "documentDefinition");
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return Task.FromResult(Failed(request, RuntimeErrorCode.RuntimeValidationBlocked, "generateDocument requires templateId/templateName/documentDefinition."));
        }

        var documentId = $"doc-{Guid.NewGuid():N}";
        var output = JsonSerializer.Serialize(new
        {
            kind = request.ActionKind,
            templateId,
            documentId,
            fileName = $"{templateId}.txt",
            status = "generated"
        }, JsonOptions);

        return Task.FromResult(new MicroflowConnectorExecutionResult
        {
            Success = true,
            Capability = request.Capability,
            OutputJson = output
        });
    }

    private static MicroflowConnectorExecutionResult Failed(MicroflowConnectorExecutionRequest request, string code, string message)
        => new()
        {
            Success = false,
            Capability = request.Capability,
            Error = new MicroflowRuntimeErrorDto
            {
                Code = code,
                Message = message,
                ObjectId = request.ObjectId,
                ActionId = request.ActionId
            }
        };

    private static Dictionary<string, JsonElement> ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            }

            var payload = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                payload[property.Name] = property.Value.Clone();
            }
            return payload;
        }
        catch
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? ReadString(Dictionary<string, JsonElement> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (payload.TryGetValue(key, out var value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString();
            }
        }

        return null;
    }
}

public sealed class DefaultExternalObjectConnector : IExternalObjectConnector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, string> Store = new(StringComparer.OrdinalIgnoreCase);

    public MicroflowConnectorCapabilityStatus GetCapabilityStatus()
        => new(MicroflowRuntimeConnectorCapability.ExternalObjectCrud, Available: true, "available");

    public Task<MicroflowConnectorExecutionResult> ExecuteAsync(
        MicroflowConnectorExecutionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = ParsePayload(request.PayloadJson);
        return request.ActionKind switch
        {
            "createExternalObject" => Task.FromResult(Create(request, payload)),
            "changeExternalObject" => Task.FromResult(Change(request, payload)),
            "deleteExternalObject" => Task.FromResult(Delete(request, payload)),
            "sendExternalObject" => Task.FromResult(Send(request, payload)),
            "externalObject" => Task.FromResult(Create(request, payload)),
            _ => Task.FromResult(Failed(request, RuntimeErrorCode.RuntimeValidationBlocked, $"Unsupported external object action: {request.ActionKind}"))
        };
    }

    private static MicroflowConnectorExecutionResult Create(MicroflowConnectorExecutionRequest request, Dictionary<string, JsonElement> payload)
    {
        var objectId = ResolveObjectId(payload) ?? $"ext-{Guid.NewGuid():N}";
        Store[objectId] = request.PayloadJson ?? "{}";
        return Success(request.Capability, new
        {
            action = request.ActionKind,
            objectId,
            status = "created"
        });
    }

    private static MicroflowConnectorExecutionResult Change(MicroflowConnectorExecutionRequest request, Dictionary<string, JsonElement> payload)
    {
        var objectId = ResolveObjectId(payload);
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return Failed(request, RuntimeErrorCode.RuntimeValidationBlocked, "changeExternalObject requires objectId/externalObjectId.");
        }

        if (!Store.ContainsKey(objectId))
        {
            return Failed(request, RuntimeErrorCode.RuntimeUnknownError, $"External object '{objectId}' not found.");
        }

        Store[objectId] = request.PayloadJson ?? "{}";
        return Success(request.Capability, new
        {
            action = request.ActionKind,
            objectId,
            status = "updated"
        });
    }

    private static MicroflowConnectorExecutionResult Delete(MicroflowConnectorExecutionRequest request, Dictionary<string, JsonElement> payload)
    {
        var objectId = ResolveObjectId(payload);
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return Failed(request, RuntimeErrorCode.RuntimeValidationBlocked, "deleteExternalObject requires objectId/externalObjectId.");
        }

        var removed = Store.TryRemove(objectId, out _);
        return Success(request.Capability, new
        {
            action = request.ActionKind,
            objectId,
            status = removed ? "deleted" : "notFound"
        });
    }

    private static MicroflowConnectorExecutionResult Send(MicroflowConnectorExecutionRequest request, Dictionary<string, JsonElement> payload)
    {
        var objectId = ResolveObjectId(payload);
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return Failed(request, RuntimeErrorCode.RuntimeValidationBlocked, "sendExternalObject requires objectId/externalObjectId.");
        }

        if (!Store.ContainsKey(objectId))
        {
            return Failed(request, RuntimeErrorCode.RuntimeUnknownError, $"External object '{objectId}' not found.");
        }

        return Success(request.Capability, new
        {
            action = request.ActionKind,
            objectId,
            status = "sent"
        });
    }

    private static MicroflowConnectorExecutionResult Success(string capability, object payload)
        => new()
        {
            Success = true,
            Capability = capability,
            OutputJson = JsonSerializer.Serialize(payload, JsonOptions)
        };

    private static MicroflowConnectorExecutionResult Failed(MicroflowConnectorExecutionRequest request, string code, string message)
        => new()
        {
            Success = false,
            Capability = request.Capability,
            Error = new MicroflowRuntimeErrorDto
            {
                Code = code,
                Message = message,
                ObjectId = request.ObjectId,
                ActionId = request.ActionId
            }
        };

    private static string? ResolveObjectId(Dictionary<string, JsonElement> payload)
        => ReadString(payload, "objectId", "externalObjectId", "id", "key");

    private static Dictionary<string, JsonElement> ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            }

            var payload = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                payload[property.Name] = property.Value.Clone();
            }
            return payload;
        }
        catch
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? ReadString(Dictionary<string, JsonElement> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (payload.TryGetValue(key, out var value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString();
            }
        }

        return null;
    }
}

