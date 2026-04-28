using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;

namespace Atlas.Application.Microflows.Services;

internal static class MicroflowSchemaJsonHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static JsonElement ParseRequired(string schemaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            return doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowSchemaInvalid, "微流 Schema JSON 无法解析。", 400, innerException: ex);
        }
    }

    public static string NormalizeAndValidate(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw SchemaInvalid("schema 必须是对象。");
        }

        if (schema.TryGetProperty("nodes", out _)
            || schema.TryGetProperty("edges", out _)
            || schema.TryGetProperty("workflowJson", out _)
            || schema.TryGetProperty("flowgram", out _))
        {
            throw SchemaInvalid("不允许保存 FlowGram JSON。");
        }

        foreach (var propertyName in new[] { "schemaVersion", "id", "name", "objectCollection", "flows", "parameters", "returnType" })
        {
            if (!schema.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                throw SchemaInvalid($"schema.{propertyName} 不能为空。");
            }
        }

        return JsonSerializer.Serialize(schema, JsonOptions);
    }

    public static string MutateFields(string schemaJson, string id, string name, string displayName, string moduleId, string? moduleName)
    {
        var node = JsonNode.Parse(schemaJson) as JsonObject
            ?? throw SchemaInvalid("微流 Schema JSON 无法解析。");
        node["id"] = id;
        node["stableId"] = id;
        node["name"] = name;
        node["displayName"] = displayName;
        node["moduleId"] = moduleId;
        node["moduleName"] = moduleName;
        return NormalizeAndValidate(JsonSerializer.SerializeToElement(node, JsonOptions));
    }

    public static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static MicroflowValidationIssueDto[] ValidateForPublish(JsonElement schema)
    {
        try
        {
            _ = NormalizeAndValidate(schema);
            return Array.Empty<MicroflowValidationIssueDto>();
        }
        catch (MicroflowApiException ex)
        {
            return
            [
                new MicroflowValidationIssueDto
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Severity = "error",
                    Code = ex.Code,
                    Message = ex.Message,
                    Source = "root",
                    FieldPath = "schema"
                }
            ];
        }
    }

    private static MicroflowApiException SchemaInvalid(string message)
        => new(MicroflowApiErrorCode.MicroflowSchemaInvalid, message, 400);
}
