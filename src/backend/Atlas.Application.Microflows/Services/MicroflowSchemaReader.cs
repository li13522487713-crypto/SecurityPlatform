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
        if (MicroflowDesignSchemaHelper.IsDesignSchema(schema))
        {
            ReadDesignWorkflow(schema.GetProperty("workflow"), model);
        }
        else if (schema.TryGetProperty("objectCollection", out var collection))
        {
            ReadCollection(collection, model, parentLoopObjectId: null, insideLoop: false, path: "objectCollection");
            if (schema.TryGetProperty("flows", out var topLevelFlows))
            {
                model.Flows.AddRange(ReadFlows(topLevelFlows, ReadString(collection, "id") ?? "root", false, "flows"));
            }
        }
        else if (schema.TryGetProperty("flows", out var rootFlows))
        {
            model.Flows.AddRange(ReadFlows(rootFlows, "root", false, "flows"));
        }

        return model;
    }

    private static void ReadDesignWorkflow(JsonElement workflow, MicroflowSchemaModel model)
    {
        if (workflow.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var nodes = workflow.TryGetProperty("nodes", out var nodeArray) && nodeArray.ValueKind == JsonValueKind.Array
            ? nodeArray.EnumerateArray().ToArray()
            : Array.Empty<JsonElement>();
        var nodeById = new Dictionary<string, MicroflowObjectModel>(StringComparer.Ordinal);

        for (var index = 0; index < nodes.Length; index++)
        {
            var node = nodes[index];
            var objectModel = ReadDesignNode(node, $"{nameof(workflow)}.nodes.{index}");
            if (string.IsNullOrWhiteSpace(objectModel.Id))
            {
                continue;
            }

            nodeById[objectModel.Id] = objectModel;
            model.Objects.Add(objectModel);
        }

        if (workflow.TryGetProperty("edges", out var edgeArray) && edgeArray.ValueKind == JsonValueKind.Array)
        {
            var edgeIndex = 0;
            foreach (var edge in edgeArray.EnumerateArray())
            {
                model.Flows.Add(ReadDesignEdge(edge, nodeById, $"{nameof(workflow)}.edges.{edgeIndex}"));
                edgeIndex++;
            }
        }
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

    private static MicroflowObjectModel ReadDesignNode(JsonElement node, string path)
    {
        var data = node.TryGetProperty("data", out var nodeData) && nodeData.ValueKind == JsonValueKind.Object
            ? nodeData
            : default;
        var propertyObject = TryReadPropertyObject(node, data);
        var objectKind = ReadString(node, "type")
            ?? ReadString(data, "objectKind")
            ?? ReadString(propertyObject, "kind")
            ?? string.Empty;
        var action = ResolveAction(node, data, propertyObject, path);
        return new MicroflowObjectModel
        {
            Id = ReadString(node, "id") ?? ReadString(data, "objectId") ?? ReadString(propertyObject, "id") ?? string.Empty,
            StableId = ReadString(propertyObject, "stableId") ?? ReadString(node, "id"),
            Kind = objectKind,
            OfficialType = ReadString(data, "officialType") ?? ReadString(propertyObject, "officialType"),
            Caption = ReadString(data, "title") ?? ReadString(propertyObject, "caption") ?? ReadString(propertyObject, "text"),
            ParameterId = ReadString(propertyObject, "parameterId"),
            CollectionId = ReadString(data, "collectionId")
                ?? ReadStringByPath(node, "meta", "collectionId")
                ?? ReadString(propertyObject, "collectionId")
                ?? "root-collection",
            ParentLoopObjectId = ResolveParentLoopObjectId(propertyObject, data),
            InsideLoop = ResolveInsideLoop(propertyObject, data),
            Action = action,
            Raw = propertyObject.ValueKind == JsonValueKind.Object ? propertyObject.Clone() : node.Clone(),
            FieldPath = path
        };
    }

    private static MicroflowActionModel? ResolveAction(JsonElement node, JsonElement data, JsonElement propertyObject, string path)
    {
        if (propertyObject.ValueKind == JsonValueKind.Object && propertyObject.TryGetProperty("action", out var propertyAction))
        {
            return ReadAction(propertyAction, path);
        }

        if (node.TryGetProperty("action", out var action))
        {
            return ReadAction(action, path);
        }

        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("action", out var dataAction))
        {
            return ReadAction(dataAction, path);
        }

        return null;
    }

    private static JsonElement TryReadPropertyObject(JsonElement node, JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("propertyObject", out var propertyObject))
        {
            return propertyObject;
        }

        return node;
    }

    private static string? ResolveParentLoopObjectId(JsonElement propertyObject, JsonElement data)
        => ReadString(propertyObject, "parentLoopObjectId")
            ?? ReadString(data, "parentLoopObjectId");

    private static bool ResolveInsideLoop(JsonElement propertyObject, JsonElement data)
        => ReadBool(propertyObject, "insideLoop")
            || ReadBool(data, "insideLoop");

    private static MicroflowFlowModel ReadDesignEdge(
        JsonElement edge,
        IReadOnlyDictionary<string, MicroflowObjectModel> nodeById,
        string path)
    {
        var data = edge.TryGetProperty("data", out var edgeData) && edgeData.ValueKind == JsonValueKind.Object
            ? edgeData
            : default;
        var propertyFlow = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("propertyFlow", out var flow)
            ? flow
            : edge;
        var sourceId = ReadString(edge, "sourceNodeID") ?? ReadString(propertyFlow, "originObjectId");
        var targetId = ReadString(edge, "targetNodeID") ?? ReadString(propertyFlow, "destinationObjectId");
        var collectionId = ReadString(data, "collectionId");
        if (string.IsNullOrWhiteSpace(collectionId)
            && !string.IsNullOrWhiteSpace(sourceId)
            && nodeById.TryGetValue(sourceId!, out var sourceNode))
        {
            collectionId = sourceNode.CollectionId;
        }

        var edgeKind = ReadString(data, "edgeKind")
            ?? ReadStringByPath(propertyFlow, "editor", "edgeKind")
            ?? ReadString(propertyFlow, "edgeKind");
        var caseValues = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("caseValues", out var values) && values.ValueKind == JsonValueKind.Array
            ? values.EnumerateArray().Select(value => value.Clone()).ToArray()
            : propertyFlow.TryGetProperty("caseValues", out var propertyCaseValues) && propertyCaseValues.ValueKind == JsonValueKind.Array
                ? propertyCaseValues.EnumerateArray().Select(value => value.Clone()).ToArray()
                : Array.Empty<JsonElement>();
        return new MicroflowFlowModel
        {
            Id = ReadString(edge, "id") ?? ReadString(data, "flowId") ?? ReadString(propertyFlow, "id") ?? string.Empty,
            Kind = ReadString(propertyFlow, "kind") ?? "sequence",
            OriginObjectId = sourceId,
            DestinationObjectId = targetId,
            EdgeKind = edgeKind,
            IsErrorHandler = ReadBool(data, "isErrorHandler")
                || ReadBool(propertyFlow, "isErrorHandler")
                || string.Equals(edgeKind, "errorHandler", StringComparison.OrdinalIgnoreCase),
            CaseValues = caseValues,
            CollectionId = collectionId ?? "root-collection",
            InsideLoop = !string.IsNullOrWhiteSpace(collectionId) && !string.Equals(collectionId, "root-collection", StringComparison.Ordinal),
            Raw = propertyFlow.ValueKind == JsonValueKind.Object ? propertyFlow.Clone() : edge.Clone(),
            FieldPath = path
        };
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
            Kind = ReadString(action, "kind") ?? ReadString(action, "actionKind") ?? string.Empty,
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
            "Number" or "number" or "Decimal" or "decimal" => MicroflowSeedMetadataCatalog.Type("decimal"),
            "Boolean" or "boolean" => MicroflowSeedMetadataCatalog.Type("boolean"),
            "DateTime" or "dateTime" => MicroflowSeedMetadataCatalog.Type("dateTime"),
            _ => MicroflowSeedMetadataCatalog.UnknownType($"unsupported dataType: {dataType}")
        };
    }

    public static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    public static string? ReadStringByPath(JsonElement element, params string[] path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var current = element;
        foreach (var part in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.GetRawText();
    }

    public static bool ReadBool(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.True;
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
