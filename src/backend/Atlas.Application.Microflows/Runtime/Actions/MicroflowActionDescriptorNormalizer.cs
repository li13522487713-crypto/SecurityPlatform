using System.Text.Json;
using System.Text.Json.Nodes;

namespace Atlas.Application.Microflows.Runtime.Actions;

public interface IMicroflowActionDescriptorNormalizer
{
    string NormalizeActionKind(string? actionKind);

    JsonElement NormalizeSchema(JsonElement schema);
}

public sealed class MicroflowActionDescriptorNormalizer : IMicroflowActionDescriptorNormalizer
{
    public static readonly IReadOnlyDictionary<string, string> LegacyToCanonical = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["webserviceCall"] = "webServiceCall",
        ["webService"] = "webServiceCall",
        ["callExternal"] = "callExternalAction",
        ["externalCall"] = "callExternalAction",
        ["deleteExternal"] = "deleteExternalObject",
        ["sendExternal"] = "sendExternalObject",
        ["rollbackObject"] = "rollback",
        ["castObject"] = "cast",
        ["listUnion"] = "listOperation",
        ["listIntersect"] = "listOperation",
        ["listSubtract"] = "listOperation",
        ["aggregate"] = "aggregateList",
        ["filter"] = "filterList",
        ["sort"] = "sortList"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string NormalizeActionKind(string? actionKind)
    {
        if (string.IsNullOrWhiteSpace(actionKind))
        {
            return string.Empty;
        }

        var trimmed = actionKind.Trim();
        return LegacyToCanonical.TryGetValue(trimmed, out var canonical)
            ? canonical
            : trimmed;
    }

    public JsonElement NormalizeSchema(JsonElement schema)
    {
        var node = JsonNode.Parse(schema.GetRawText());
        if (node is null)
        {
            return schema.Clone();
        }

        NormalizeNode(node);
        return JsonSerializer.SerializeToElement(node, JsonOptions);
    }

    private void NormalizeNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            NormalizeObject(obj);
            foreach (var child in obj.Select(static pair => pair.Value).Where(static value => value is not null).ToArray())
            {
                NormalizeNode(child!);
            }
            return;
        }

        if (node is JsonArray array)
        {
            foreach (var child in array.Where(static value => value is not null).ToArray())
            {
                NormalizeNode(child!);
            }
        }
    }

    private void NormalizeObject(JsonObject obj)
    {
        NormalizeProperty(obj, "actionKind");
        NormalizeProperty(obj, "kind");
        if (obj["action"] is JsonObject action)
        {
            NormalizeProperty(action, "kind");
            NormalizeProperty(action, "actionKind");
        }

        var normalizedKind = ReadString(obj, "actionKind") ?? ReadString(obj, "kind");
        if (!string.Equals(normalizedKind, "listOperation", StringComparison.Ordinal))
        {
            return;
        }

        var operation = ReadString(obj, "operation");
        if (string.IsNullOrWhiteSpace(operation))
        {
            var originalKind = ReadString(obj, "legacyKind") ?? ReadString(obj, "sourceKind");
            operation = originalKind switch
            {
                "listUnion" => "union",
                "listIntersect" => "intersect",
                "listSubtract" => "subtract",
                _ => null
            };
        }

        if (!string.IsNullOrWhiteSpace(operation))
        {
            obj["operation"] = operation;
        }
    }

    private void NormalizeProperty(JsonObject obj, string propertyName)
    {
        if (obj[propertyName] is not JsonValue value || !value.TryGetValue<string>(out var raw))
        {
            return;
        }

        var canonical = NormalizeActionKind(raw);
        if (!string.Equals(canonical, raw, StringComparison.Ordinal))
        {
            obj["legacyKind"] ??= raw;
            obj[propertyName] = canonical;
        }
    }

    private static string? ReadString(JsonObject obj, string propertyName)
        => obj[propertyName] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
}
