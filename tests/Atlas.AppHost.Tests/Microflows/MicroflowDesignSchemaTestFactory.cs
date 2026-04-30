using System.Text.Json;
using System.Text.Json.Nodes;

namespace Atlas.AppHost.Tests.Microflows;

internal static class MicroflowDesignSchemaTestFactory
{
    public static JsonElement Schema(
        IReadOnlyList<object> objects,
        IReadOnlyList<object> flows,
        IReadOnlyList<object>? parameters,
        string id,
        JsonSerializerOptions options)
    {
        var nodes = new JsonArray();
        for (var index = 0; index < objects.Count; index++)
        {
            nodes.Add(CreateNode(objects[index], index, options));
        }

        var edges = new JsonArray();
        for (var index = 0; index < flows.Count; index++)
        {
            edges.Add(CreateEdge(flows[index], index, options));
        }

        var root = new JsonObject
        {
            ["schemaVersion"] = "flowgram.microflow.v1",
            ["id"] = id,
            ["name"] = id,
            ["displayName"] = id,
            ["moduleId"] = "mod",
            ["moduleName"] = "TestModule",
            ["parameters"] = JsonSerializer.SerializeToNode(parameters ?? Array.Empty<object>(), options),
            ["returnType"] = Type("unknown"),
            ["workflow"] = new JsonObject
            {
                ["nodes"] = nodes,
                ["edges"] = edges
            },
            ["editor"] = new JsonObject(),
            ["audit"] = new JsonObject()
        };

        return JsonSerializer.SerializeToElement(root, options);
    }

    private static JsonObject CreateNode(object source, int index, JsonSerializerOptions options)
    {
        var raw = ToObject(source, options);
        var id = ReadString(raw, "id") ?? $"node-{index}";
        var kind = ReadString(raw, "kind") ?? "actionActivity";
        var caption = ReadString(raw, "caption") ?? id;
        var data = new JsonObject
        {
            ["objectId"] = id,
            ["objectKind"] = kind,
            ["collectionId"] = "root-collection",
            ["title"] = caption,
            ["officialType"] = ReadString(raw, "officialType") ?? OfficialType(kind),
            ["disabled"] = false,
            ["validationState"] = "valid",
            ["runtimeState"] = "idle",
            ["issueCount"] = 0
        };

        CopyProperties(raw, data, "id", "kind", "caption", "objectCollection");

        return new JsonObject
        {
            ["id"] = id,
            ["type"] = kind,
            ["data"] = data,
            ["meta"] = new JsonObject
            {
                ["nodeDTOType"] = "microflow",
                ["collectionId"] = "root-collection",
                ["position"] = new JsonObject
                {
                    ["x"] = 120 + index * 180,
                    ["y"] = 120
                },
                ["size"] = new JsonObject
                {
                    ["width"] = 180,
                    ["height"] = 56
                }
            }
        };
    }

    private static JsonObject CreateEdge(object source, int index, JsonSerializerOptions options)
    {
        var raw = ToObject(source, options);
        var id = ReadString(raw, "id") ?? $"edge-{index}";
        var edgeKind = ReadStringByPath(raw, "editor", "edgeKind")
            ?? (ReadBool(raw, "isErrorHandler") ? "errorHandler" : "sequence");
        var data = new JsonObject
        {
            ["flowId"] = id,
            ["flowKind"] = ReadString(raw, "kind") ?? "sequence",
            ["edgeKind"] = edgeKind,
            ["collectionId"] = "root-collection",
            ["isErrorHandler"] = ReadBool(raw, "isErrorHandler")
        };

        CopyProperties(raw, data, "id", "kind", "originObjectId", "destinationObjectId", "editor");
        if (raw.TryGetPropertyValue("editor", out var editor) && editor is JsonObject editorObject)
        {
            CopyProperties(editorObject, data, "edgeKind");
        }

        return new JsonObject
        {
            ["id"] = id,
            ["sourceNodeID"] = ReadString(raw, "originObjectId") ?? string.Empty,
            ["targetNodeID"] = ReadString(raw, "destinationObjectId") ?? string.Empty,
            ["data"] = data
        };
    }

    private static JsonObject ToObject(object source, JsonSerializerOptions options)
        => JsonSerializer.SerializeToNode(source, options) as JsonObject
           ?? throw new InvalidOperationException("Microflow test schema part must serialize to a JSON object.");

    private static void CopyProperties(JsonObject source, JsonObject target, params string[] excluded)
    {
        var excludedSet = excluded.ToHashSet(StringComparer.Ordinal);
        foreach (var property in source)
        {
            if (excludedSet.Contains(property.Key))
            {
                continue;
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }

    private static JsonObject Type(string kind)
        => new()
        {
            ["kind"] = kind
        };

    private static string OfficialType(string kind)
        => kind switch
        {
            "startEvent" => "Microflows$StartEvent",
            "endEvent" => "Microflows$EndEvent",
            "actionActivity" => "Microflows$ActionActivity",
            "exclusiveSplit" => "Microflows$ExclusiveSplit",
            "exclusiveMerge" => "Microflows$ExclusiveMerge",
            "loopedActivity" => "Microflows$LoopedActivity",
            "errorEvent" => "Microflows$ErrorEvent",
            "parallelSplit" => "Microflows$ParallelSplit",
            "parallelMerge" => "Microflows$ParallelMerge",
            "annotation" => "Microflows$Annotation",
            _ => $"Microflows${kind}"
        };

    private static string? ReadString(JsonObject obj, string propertyName)
        => obj.TryGetPropertyValue(propertyName, out var value) && value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text
            : null;

    private static string? ReadStringByPath(JsonObject obj, params string[] path)
    {
        JsonNode? current = obj;
        foreach (var part in path)
        {
            if (current is not JsonObject currentObject || !currentObject.TryGetPropertyValue(part, out current))
            {
                return null;
            }
        }

        return current is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    }

    private static bool ReadBool(JsonObject obj, string propertyName)
        => obj.TryGetPropertyValue(propertyName, out var value) && value is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out var result) && result;
}
