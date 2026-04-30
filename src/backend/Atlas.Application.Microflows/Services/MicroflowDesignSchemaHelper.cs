using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;

namespace Atlas.Application.Microflows.Services;

internal static class MicroflowDesignSchemaHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal const string DesignSchemaVersion = "flowgram.microflow.v1";

    public static bool IsDesignSchema(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!schema.TryGetProperty("schemaVersion", out var version)
            || version.ValueKind != JsonValueKind.String
            || !string.Equals(version.GetString(), DesignSchemaVersion, StringComparison.Ordinal))
        {
            return false;
        }

        return schema.TryGetProperty("workflow", out var workflow)
            && workflow.ValueKind == JsonValueKind.Object
            && workflow.TryGetProperty("nodes", out var nodes)
            && nodes.ValueKind == JsonValueKind.Array
            && workflow.TryGetProperty("edges", out var edges)
            && edges.ValueKind == JsonValueKind.Array;
    }

    public static JsonElement EnsureDesignSchema(JsonElement schema, string? errorMessage = null)
    {
        if (!IsDesignSchema(schema))
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowSchemaInvalid,
                errorMessage ?? "当前微流仍是旧设计态快照，无法在新版 Studio 中打开。",
                422);
        }

        return schema.Clone();
    }

    public static JsonElement? TryParseStoredDesignSchema(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            return IsDesignSchema(document.RootElement) ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static JsonElement ParseStoredDesignSchema(string schemaJson, string? errorMessage = null)
    {
        try
        {
            using var document = JsonDocument.Parse(schemaJson);
            return EnsureDesignSchema(document.RootElement, errorMessage);
        }
        catch (JsonException ex)
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowSchemaInvalid,
                errorMessage ?? "微流设计态 schema 无法解析。",
                422,
                innerException: ex);
        }
    }

    public static string NormalizeAndValidateDesignSchema(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw SchemaInvalid("schema 必须是对象。");
        }

        if (schema.TryGetProperty("objectCollection", out _)
            || schema.TryGetProperty("flows", out _)
            || schema.TryGetProperty("nodes", out _)
            || schema.TryGetProperty("edges", out _)
            || schema.TryGetProperty("workflowJson", out _)
            || schema.TryGetProperty("flowgram", out _))
        {
            throw SchemaInvalid("微流设计态必须使用 workflow.nodes/workflow.edges，禁止保存旧 objectCollection/flows 或裸 FlowGram JSON。");
        }

        RequireProperty(schema, "schemaVersion");
        if (!string.Equals(schema.GetProperty("schemaVersion").GetString(), DesignSchemaVersion, StringComparison.Ordinal))
        {
            throw SchemaInvalid($"schema.schemaVersion 必须为 {DesignSchemaVersion}。");
        }

        RequireProperty(schema, "id");
        RequireProperty(schema, "name");
        RequireProperty(schema, "moduleId");
        RequireProperty(schema, "workflow");
        RequireProperty(schema, "parameters");
        RequireProperty(schema, "returnType");

        var workflow = schema.GetProperty("workflow");
        if (workflow.ValueKind != JsonValueKind.Object)
        {
            throw SchemaInvalid("workflow 必须是对象。");
        }

        RequireProperty(workflow, "nodes");
        RequireProperty(workflow, "edges");
        if (workflow.GetProperty("nodes").ValueKind != JsonValueKind.Array)
        {
            throw SchemaInvalid("workflow.nodes 必须是数组。");
        }

        if (workflow.GetProperty("edges").ValueKind != JsonValueKind.Array)
        {
            throw SchemaInvalid("workflow.edges 必须是数组。");
        }

        foreach (var node in workflow.GetProperty("nodes").EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.Object)
            {
                throw SchemaInvalid("workflow.nodes 项必须是对象。");
            }

            RequireProperty(node, "id");
            RequireProperty(node, "type");
            RequireProperty(node, "meta");
            var meta = node.GetProperty("meta");
            if (meta.ValueKind != JsonValueKind.Object
                || !meta.TryGetProperty("position", out var position)
                || position.ValueKind != JsonValueKind.Object)
            {
                throw SchemaInvalid("workflow.nodes[].meta.position 不能为空。");
            }
        }

        foreach (var edge in workflow.GetProperty("edges").EnumerateArray())
        {
            if (edge.ValueKind != JsonValueKind.Object)
            {
                throw SchemaInvalid("workflow.edges 项必须是对象。");
            }

            RequireProperty(edge, "sourceNodeID");
            RequireProperty(edge, "targetNodeID");
        }

        return JsonSerializer.Serialize(schema, JsonOptions);
    }

    public static string MutateDesignSchemaFields(
        string schemaJson,
        string id,
        string name,
        string displayName,
        string moduleId,
        string? moduleName)
    {
        var node = JsonNode.Parse(schemaJson) as JsonObject
            ?? throw SchemaInvalid("微流设计态 schema 无法解析。");
        if (node["schemaVersion"]?.GetValue<string>() != DesignSchemaVersion)
        {
            throw SchemaInvalid("当前微流仍是旧设计态快照，无法在新版 Studio 中打开。");
        }

        node["id"] = id;
        node["stableId"] = id;
        node["name"] = name;
        node["displayName"] = displayName;
        node["moduleId"] = moduleId;
        node["moduleName"] = moduleName;
        return NormalizeAndValidateDesignSchema(JsonSerializer.SerializeToElement(node, JsonOptions));
    }

    private static void RequireProperty(JsonElement schema, string propertyName)
    {
        if (!schema.TryGetProperty(propertyName, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            throw SchemaInvalid($"schema.{propertyName} 不能为空。");
        }
    }

    private static MicroflowApiException SchemaInvalid(string message)
        => new(MicroflowApiErrorCode.MicroflowSchemaInvalid, message, 422);
}
