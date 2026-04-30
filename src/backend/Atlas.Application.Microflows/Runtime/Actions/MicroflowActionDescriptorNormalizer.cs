using System.Text.Json;
using System.Text.Json.Nodes;

namespace Atlas.Application.Microflows.Runtime.Actions;

public interface IMicroflowActionDescriptorNormalizer
{
    string NormalizeActionKind(string? actionKind);

    JsonElement NormalizeSchema(JsonElement schema);
}

public sealed record MicroflowActionDescriptorNormalizationChange
{
    public string Path { get; init; } = string.Empty;

    public string Original { get; init; } = string.Empty;

    public string Canonical { get; init; } = string.Empty;
}

public sealed record MicroflowActionDescriptorNormalizationResult
{
    public string Original { get; init; } = string.Empty;

    public string Canonical { get; init; } = string.Empty;

    public bool Changed { get; init; }

    public IReadOnlyList<MicroflowActionDescriptorNormalizationChange> Changes { get; init; } = Array.Empty<MicroflowActionDescriptorNormalizationChange>();
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

    public MicroflowActionDescriptorNormalizationResult Normalize(string? actionKind, string path)
    {
        var original = actionKind?.Trim() ?? string.Empty;
        var canonical = NormalizeActionKind(actionKind);
        var changed = !string.Equals(original, canonical, StringComparison.Ordinal);
        return new MicroflowActionDescriptorNormalizationResult
        {
            Original = original,
            Canonical = canonical,
            Changed = changed,
            Changes = changed
                ? [new MicroflowActionDescriptorNormalizationChange { Path = path, Original = original, Canonical = canonical }]
                : Array.Empty<MicroflowActionDescriptorNormalizationChange>()
        };
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
            NormalizeListFamilyProperties(obj, normalizedKind);
            return;
        }

        NormalizeListOperation(obj);
        NormalizeListFamilyProperties(obj, normalizedKind);
    }

    private void NormalizeListOperation(JsonObject obj)
    {
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

    private void NormalizeListFamilyProperties(JsonObject obj, string? normalizedKind)
    {
        switch (normalizedKind)
        {
            case "createList":
                CopyIfMissing(obj, "outputListVariableName", "outputVariableName", "resultVariableName", "listVariableName");
                break;
            case "changeList":
                CopyIfMissing(obj, "targetListVariableName", "targetVariableName", "listVariableName");
                CopyIfMissing(obj, "sourceListVariableName", "sourceVariableName");
                break;
            case "aggregateList":
                CopyIfMissing(obj, "sourceListVariableName", "sourceVariableName", "listVariableName");
                CopyIfMissing(obj, "aggregateFunction", "aggregate", "operation");
                NormalizeAggregateFunction(obj, "aggregateFunction");
                break;
            case "listOperation":
                CopyIfMissing(obj, "leftListVariableName", "inputListVariable", "inputListVariableName", "listVariableName", "sourceVariableName", "leftVariableName");
                CopyIfMissing(obj, "sourceListVariableName", "leftListVariableName");
                CopyIfMissing(obj, "rightListVariableName", "otherListVariableName", "secondListVariable", "secondListVariableName", "rightVariableName");
                CopyIfMissing(obj, "outputListVariableName", "outputVariableName", "resultVariableName");
                break;
        }
    }

    private static void NormalizeAggregateFunction(JsonObject obj, string propertyName)
    {
        var raw = ReadString(obj, propertyName);
        if (raw is null)
        {
            return;
        }

        var canonical = raw switch
        {
            "minimum" => "min",
            "maximum" => "max",
            _ => raw
        };

        if (!string.Equals(raw, canonical, StringComparison.Ordinal))
        {
            obj[propertyName] = canonical;
        }
    }

    private static void CopyIfMissing(JsonObject obj, string targetPropertyName, params string[] sourcePropertyNames)
    {
        if (!string.IsNullOrWhiteSpace(ReadString(obj, targetPropertyName)))
        {
            return;
        }

        foreach (var sourcePropertyName in sourcePropertyNames)
        {
            if (obj[sourcePropertyName] is JsonValue value)
            {
                obj[targetPropertyName] = value.GetValue<string>();
                return;
            }
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
