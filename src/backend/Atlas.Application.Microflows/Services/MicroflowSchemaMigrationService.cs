using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Runtime.Actions;

namespace Atlas.Application.Microflows.Services;

public interface IMicroflowSchemaMigrationService
{
    MicroflowSchemaMigrationResult NormalizeForLoad(string schemaJson);

    MicroflowSchemaMigrationResult NormalizeForSave(JsonElement schema);

    MicroflowSchemaMigrationResult NormalizeForPublish(JsonElement schema);
}

public sealed record MicroflowSchemaMigrationResult
{
    public JsonElement Schema { get; init; }

    public string SchemaJson { get; init; } = "{}";

    public bool Changed { get; init; }

    public IReadOnlyList<MicroflowActionDescriptorNormalizationChange> Changes { get; init; } = Array.Empty<MicroflowActionDescriptorNormalizationChange>();
}

public sealed class MicroflowSchemaMigrationService : IMicroflowSchemaMigrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MicroflowActionDescriptorNormalizer _normalizer;

    public MicroflowSchemaMigrationService()
        : this(new MicroflowActionDescriptorNormalizer())
    {
    }

    public MicroflowSchemaMigrationService(MicroflowActionDescriptorNormalizer normalizer)
    {
        _normalizer = normalizer;
    }

    public static JsonElement NormalizeElement(JsonElement schema)
        => new MicroflowSchemaMigrationService().NormalizeForSave(schema).Schema;

    public static string NormalizeJson(string schemaJson)
        => new MicroflowSchemaMigrationService().NormalizeForLoad(schemaJson).SchemaJson;

    public MicroflowSchemaMigrationResult NormalizeForLoad(string schemaJson)
    {
        try
        {
            var node = JsonNode.Parse(schemaJson) as JsonObject
                ?? throw MigrationFailed("微流 Schema JSON 必须是对象。");
            return NormalizeNode(node);
        }
        catch (JsonException ex)
        {
            throw MigrationFailed("微流 Schema JSON 无法解析。", ex);
        }
    }

    public MicroflowSchemaMigrationResult NormalizeForSave(JsonElement schema)
    {
        var node = JsonSerializer.SerializeToNode(schema, JsonOptions) as JsonObject
            ?? throw MigrationFailed("微流 Schema 必须是对象。");
        return NormalizeNode(node);
    }

    public MicroflowSchemaMigrationResult NormalizeForPublish(JsonElement schema)
    {
        try
        {
            return NormalizeForSave(schema);
        }
        catch (MicroflowApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw MigrationFailed("微流 Schema 迁移失败，已阻断发布。", ex);
        }
    }

    private MicroflowSchemaMigrationResult NormalizeNode(JsonObject root)
    {
        var changes = new List<MicroflowActionDescriptorNormalizationChange>();
        NormalizeRecursive(root, "$", changes);
        var schemaElement = JsonSerializer.SerializeToElement(root, JsonOptions);
        var schemaJson = MicroflowSchemaJsonHelper.NormalizeAndValidate(schemaElement);
        using var document = JsonDocument.Parse(schemaJson);
        return new MicroflowSchemaMigrationResult
        {
            Schema = document.RootElement.Clone(),
            SchemaJson = schemaJson,
            Changed = changes.Count > 0,
            Changes = changes
        };
    }

    private void NormalizeRecursive(JsonNode? node, string path, List<MicroflowActionDescriptorNormalizationChange> changes)
    {
        switch (node)
        {
            case JsonObject obj:
                NormalizeObject(obj, path, changes);
                foreach (var property in obj.ToArray())
                {
                    NormalizeRecursive(property.Value, $"{path}.{property.Key}", changes);
                }
                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    NormalizeRecursive(array[index], $"{path}[{index}]", changes);
                }
                break;
        }
    }

    private void NormalizeObject(JsonObject obj, string path, List<MicroflowActionDescriptorNormalizationChange> changes)
    {
        NormalizeStringProperty(obj, "kind", path, changes);
        NormalizeStringProperty(obj, "actionKind", path, changes);
        if (obj["action"] is JsonObject action)
        {
            NormalizeStringProperty(action, "kind", $"{path}.action", changes);
            NormalizeStringProperty(action, "actionKind", $"{path}.action", changes);
        }
    }

    private void NormalizeStringProperty(JsonObject obj, string propertyName, string path, List<MicroflowActionDescriptorNormalizationChange> changes)
    {
        if (obj[propertyName] is not JsonValue value || !value.TryGetValue<string>(out var raw))
        {
            return;
        }

        var result = _normalizer.Normalize(raw, $"{path}.{propertyName}");
        if (!result.Changed)
        {
            return;
        }

        obj[propertyName] = result.Canonical;
        changes.AddRange(result.Changes);

        if (string.Equals(result.Canonical, "listOperation", StringComparison.Ordinal)
            && !obj.ContainsKey("operation"))
        {
            var operation = result.Original switch
            {
                "listUnion" => "union",
                "listIntersect" => "intersect",
                "listSubtract" => "subtract",
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(operation))
            {
                obj["operation"] = operation;
            }
        }
    }

    private static MicroflowApiException MigrationFailed(string message, Exception? inner = null)
        => new("MIGRATION_FAILED", message, 422, innerException: inner);
}
