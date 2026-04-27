using System.Text.Json;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Models;

namespace Atlas.Application.Microflows.Services;

public interface IMicroflowSchemaReader
{
    MicroflowSchemaModel Read(JsonElement schema);
}

public sealed class MicroflowSchemaReader : IMicroflowSchemaReader
{
    public MicroflowSchemaModel Read(JsonElement schema)
    {
        var model = new MicroflowSchemaModel
        {
            Root = schema.Clone(),
            SchemaVersion = ReadString(schema, "schemaVersion"),
            Id = ReadString(schema, "id"),
            Name = ReadString(schema, "name"),
            DisplayName = ReadString(schema, "displayName"),
            ModuleId = ReadString(schema, "moduleId"),
            ReturnType = schema.TryGetProperty("returnType", out var returnType) ? returnType.Clone() : null
        };

        model.Parameters.AddRange(ReadParameters(schema));
        if (schema.TryGetProperty("objectCollection", out var collection))
        {
            ReadCollection(collection, model, parentLoopObjectId: null, insideLoop: false, path: "objectCollection");
        }
        else if (schema.TryGetProperty("flows", out var rootFlows))
        {
            model.Flows.AddRange(ReadFlows(rootFlows, "root", false, "flows"));
        }

        return model;
    }

    private static IEnumerable<MicroflowParameterModel> ReadParameters(JsonElement schema)
    {
        if (!schema.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var index = 0;
        foreach (var parameter in parameters.EnumerateArray())
        {
            yield return new MicroflowParameterModel
            {
                Id = ReadString(parameter, "id") ?? ReadString(parameter, "name") ?? $"parameter-{index}",
                Name = ReadString(parameter, "name") ?? string.Empty,
                Type = ReadType(parameter),
                Raw = parameter.Clone(),
                FieldPath = $"parameters.{index}"
            };
            index++;
        }
    }

    private static void ReadCollection(JsonElement collection, MicroflowSchemaModel model, string? parentLoopObjectId, bool insideLoop, string path)
    {
        var collectionId = ReadString(collection, "id") ?? path;
        if (collection.TryGetProperty("objects", out var objects) && objects.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var obj in objects.EnumerateArray())
            {
                var objectModel = ReadObject(obj, collectionId, parentLoopObjectId, insideLoop, $"{path}.objects.{index}");
                model.Objects.Add(objectModel);
                if (obj.TryGetProperty("objectCollection", out var nested)
                    || obj.TryGetProperty("containedObjectCollection", out nested)
                    || obj.TryGetProperty("loopObjectCollection", out nested))
                {
                    ReadCollection(nested, model, objectModel.Id, true, $"{objectModel.FieldPath}.objectCollection");
                }

                index++;
            }
        }

        if (collection.TryGetProperty("flows", out var flows))
        {
            model.Flows.AddRange(ReadFlows(flows, collectionId, insideLoop, $"{path}.flows"));
        }
    }

    private static MicroflowObjectModel ReadObject(JsonElement obj, string collectionId, string? parentLoopObjectId, bool insideLoop, string path)
    {
        var action = obj.TryGetProperty("action", out var actionElement) ? ReadAction(actionElement, path) : null;
        return new MicroflowObjectModel
        {
            Id = ReadString(obj, "id") ?? string.Empty,
            StableId = ReadString(obj, "stableId"),
            Kind = ReadString(obj, "kind") ?? string.Empty,
            OfficialType = ReadString(obj, "officialType"),
            Caption = ReadString(obj, "caption"),
            ParameterId = ReadString(obj, "parameterId"),
            CollectionId = collectionId,
            ParentLoopObjectId = parentLoopObjectId,
            InsideLoop = insideLoop,
            Action = action,
            Raw = obj.Clone(),
            FieldPath = path
        };
    }

    private static MicroflowActionModel ReadAction(JsonElement action, string objectPath)
        => new()
        {
            Id = ReadString(action, "id") ?? string.Empty,
            Kind = ReadString(action, "kind") ?? string.Empty,
            OfficialType = ReadString(action, "officialType"),
            Raw = action.Clone(),
            FieldPath = $"{objectPath}.action"
        };

    private static IEnumerable<MicroflowFlowModel> ReadFlows(JsonElement flows, string collectionId, bool insideLoop, string path)
    {
        if (flows.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var index = 0;
        foreach (var flow in flows.EnumerateArray())
        {
            var edgeKind = ReadStringByPath(flow, "editor", "edgeKind") ?? ReadString(flow, "edgeKind");
            yield return new MicroflowFlowModel
            {
                Id = ReadString(flow, "id") ?? string.Empty,
                Kind = ReadString(flow, "kind") ?? "sequence",
                OriginObjectId = ReadString(flow, "originObjectId"),
                DestinationObjectId = ReadString(flow, "destinationObjectId"),
                EdgeKind = edgeKind,
                IsErrorHandler = ReadBool(flow, "isErrorHandler") || string.Equals(edgeKind, "errorHandler", StringComparison.OrdinalIgnoreCase),
                CaseValues = flow.TryGetProperty("caseValues", out var caseValues) && caseValues.ValueKind == JsonValueKind.Array
                    ? caseValues.EnumerateArray().Select(value => value.Clone()).ToArray()
                    : Array.Empty<JsonElement>(),
                CollectionId = collectionId,
                InsideLoop = insideLoop,
                Raw = flow.Clone(),
                FieldPath = $"{path}.{index}"
            };
            index++;
        }
    }

    private static JsonElement ReadType(JsonElement element)
    {
        if (element.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.Object)
        {
            return type.Clone();
        }

        var dataType = ReadString(element, "dataType") ?? "unknown";
        return dataType switch
        {
            "String" or "string" => MicroflowSeedMetadataCatalog.Type("string"),
            "Integer" or "integer" or "Int" => MicroflowSeedMetadataCatalog.Type("integer"),
            "Long" or "long" => MicroflowSeedMetadataCatalog.Type("long"),
            "Decimal" or "decimal" => MicroflowSeedMetadataCatalog.Type("decimal"),
            "Boolean" or "boolean" => MicroflowSeedMetadataCatalog.Type("boolean"),
            "DateTime" or "dateTime" => MicroflowSeedMetadataCatalog.Type("dateTime"),
            _ => MicroflowSeedMetadataCatalog.UnknownType($"unsupported dataType: {dataType}")
        };
    }

    public static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public static string? ReadStringByPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (!current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.GetRawText();
    }

    public static bool ReadBool(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;
}

public sealed class MicroflowValidationContext
{
    public string ResourceId { get; init; } = string.Empty;

    public JsonElement Schema { get; init; }

    public MicroflowSchemaModel SchemaModel { get; init; } = new();

    public MicroflowMetadataCatalogDto Metadata { get; init; } = new();

    public string Mode { get; init; } = "edit";

    public bool IncludeWarnings { get; init; } = true;

    public bool IncludeInfo { get; init; }

    public List<MicroflowValidationIssueDto> Issues { get; } = [];
}

public sealed class MicroflowSchemaModel
{
    public JsonElement Root { get; init; }
    public string? SchemaVersion { get; init; }
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? DisplayName { get; init; }
    public string? ModuleId { get; init; }
    public JsonElement? ReturnType { get; init; }
    public List<MicroflowParameterModel> Parameters { get; } = [];
    public List<MicroflowObjectModel> Objects { get; } = [];
    public List<MicroflowFlowModel> Flows { get; } = [];
}

public sealed record MicroflowParameterModel
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public JsonElement Type { get; init; }
    public JsonElement Raw { get; init; }
    public string FieldPath { get; init; } = string.Empty;
}

public sealed record MicroflowObjectModel
{
    public string Id { get; init; } = string.Empty;
    public string? StableId { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string? OfficialType { get; init; }
    public string? Caption { get; init; }
    public string? ParameterId { get; init; }
    public string CollectionId { get; init; } = string.Empty;
    public string? ParentLoopObjectId { get; init; }
    public bool InsideLoop { get; init; }
    public MicroflowActionModel? Action { get; init; }
    public JsonElement Raw { get; init; }
    public string FieldPath { get; init; } = string.Empty;
}

public sealed record MicroflowActionModel
{
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string? OfficialType { get; init; }
    public JsonElement Raw { get; init; }
    public string FieldPath { get; init; } = string.Empty;
}

public sealed record MicroflowFlowModel
{
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = "sequence";
    public string? OriginObjectId { get; init; }
    public string? DestinationObjectId { get; init; }
    public string? EdgeKind { get; init; }
    public bool IsErrorHandler { get; init; }
    public IReadOnlyList<JsonElement> CaseValues { get; init; } = Array.Empty<JsonElement>();
    public string CollectionId { get; init; } = string.Empty;
    public bool InsideLoop { get; init; }
    public JsonElement Raw { get; init; }
    public string FieldPath { get; init; } = string.Empty;
}
