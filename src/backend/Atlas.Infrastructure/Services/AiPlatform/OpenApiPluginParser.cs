using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class OpenApiPluginParser : IOpenApiPluginParser
{
    private static readonly HashSet<string> SupportedHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"
    };

    public Task<OpenApiPluginParseResult> ParseAsync(string openApiJson, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(openApiJson))
        {
            throw new BusinessException("OpenAPI 内容不能为空。", ErrorCodes.ValidationError);
        }

        JsonNode openApiNode;
        try
        {
            openApiNode = JsonNode.Parse(openApiJson)
                ?? throw new BusinessException("OpenAPI 内容不能为空。", ErrorCodes.ValidationError);
        }
        catch (JsonException)
        {
            throw new BusinessException("OpenAPI JSON 格式无效。", ErrorCodes.ValidationError);
        }

        if (openApiNode is not JsonObject openApiObject)
        {
            throw new BusinessException("OpenAPI 根节点必须是对象。", ErrorCodes.ValidationError);
        }

        var pathsNode = openApiObject["paths"] as JsonObject;
        if (pathsNode is null || pathsNode.Count == 0)
        {
            throw new BusinessException("OpenAPI 缺少 paths 定义。", ErrorCodes.ValidationError);
        }

        var toolArray = new JsonArray();
        var toolItems = new List<FunctionToolSchemaItem>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pathEntry in pathsNode)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pathEntry.Value is not JsonObject pathObject)
            {
                continue;
            }

            foreach (var methodEntry in pathObject)
            {
                if (!SupportedHttpMethods.Contains(methodEntry.Key))
                {
                    continue;
                }

                if (methodEntry.Value is not JsonObject operationObject)
                {
                    continue;
                }

                var method = methodEntry.Key.ToUpperInvariant();
                var operationName = BuildOperationName(pathEntry.Key, method, operationObject, usedNames);
                var operationDescription = operationObject["summary"]?.GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(operationDescription))
                {
                    operationDescription = operationObject["description"]?.GetValue<string>()?.Trim();
                }

                operationDescription = string.IsNullOrWhiteSpace(operationDescription)
                    ? $"{method} {pathEntry.Key}"
                    : operationDescription;

                var parameterSchema = BuildParameterSchema(pathObject, operationObject, out var requestSchemaJson);
                var responseSchemaJson = operationObject["responses"]?.ToJsonString();

                var functionNode = new JsonObject
                {
                    ["name"] = operationName,
                    ["description"] = operationDescription,
                    ["parameters"] = parameterSchema
                };

                var toolNode = new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = functionNode
                };

                toolArray.Add(toolNode);
                toolItems.Add(new FunctionToolSchemaItem(
                    operationName,
                    operationDescription,
                    method,
                    pathEntry.Key,
                    toolNode.ToJsonString(),
                    requestSchemaJson,
                    responseSchemaJson));
            }
        }

        if (toolItems.Count == 0)
        {
            throw new BusinessException("OpenAPI 未解析到可导入接口。", ErrorCodes.ValidationError);
        }

        return Task.FromResult(new OpenApiPluginParseResult(
            openApiObject.ToJsonString(),
            toolArray.ToJsonString(),
            toolItems));
    }

    private static JsonObject BuildParameterSchema(
        JsonObject pathObject,
        JsonObject operationObject,
        out string? requestSchemaJson)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        AppendParameters(pathObject["parameters"] as JsonArray, properties, required);
        AppendParameters(operationObject["parameters"] as JsonArray, properties, required);

        requestSchemaJson = null;
        if (operationObject["requestBody"] is JsonObject requestBody)
        {
            requestSchemaJson = requestBody.ToJsonString();
            var bodySchema = ResolveRequestBodySchema(requestBody);
            if (bodySchema is not null)
            {
                properties["body"] = bodySchema.DeepClone();
                if (requestBody["required"]?.GetValue<bool>() == true && !required.Any(x => x?.GetValue<string>() == "body"))
                {
                    required.Add("body");
                }
            }
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    private static void AppendParameters(JsonArray? parameters, JsonObject properties, JsonArray required)
    {
        if (parameters is null)
        {
            return;
        }

        foreach (var parameterNode in parameters)
        {
            if (parameterNode is not JsonObject parameterObject)
            {
                continue;
            }

            var name = parameterObject["name"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var schemaObject = parameterObject["schema"] as JsonObject;
            JsonNode propertySchema = schemaObject?.DeepClone()
                ?? new JsonObject
                {
                    ["type"] = "string"
                };

            if (propertySchema is JsonObject propertySchemaObject)
            {
                if (propertySchemaObject["description"] is null
                    && parameterObject["description"] is JsonValue descriptionNode
                    && descriptionNode.TryGetValue<string>(out var description)
                    && !string.IsNullOrWhiteSpace(description))
                {
                    propertySchemaObject["description"] = description;
                }
            }

            properties[name] = propertySchema;
            if (parameterObject["required"]?.GetValue<bool>() == true
                && !required.Any(x => string.Equals(x?.GetValue<string>(), name, StringComparison.Ordinal)))
            {
                required.Add(name);
            }
        }
    }

    private static JsonNode? ResolveRequestBodySchema(JsonObject requestBody)
    {
        if (requestBody["content"] is not JsonObject contentNode || contentNode.Count == 0)
        {
            return null;
        }

        var jsonContent = contentNode["application/json"] as JsonObject;
        if (jsonContent?["schema"] is JsonNode schemaNode)
        {
            return schemaNode;
        }

        foreach (var contentEntry in contentNode)
        {
            if (contentEntry.Value is not JsonObject mediaType)
            {
                continue;
            }

            if (mediaType["schema"] is JsonNode fallbackSchema)
            {
                return fallbackSchema;
            }
        }

        return null;
    }

    private static string BuildOperationName(
        string path,
        string method,
        JsonObject operationObject,
        HashSet<string> usedNames)
    {
        var preferredName = operationObject["operationId"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(preferredName))
        {
            preferredName = $"{method}_{path}";
        }

        var sanitized = Regex.Replace(preferredName, "[^a-zA-Z0-9_]", "_");
        sanitized = Regex.Replace(sanitized, "_{2,}", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = $"{method}_tool";
        }

        if (!char.IsLetter(sanitized[0]) && sanitized[0] != '_')
        {
            sanitized = $"tool_{sanitized}";
        }

        if (usedNames.Add(sanitized))
        {
            return sanitized;
        }

        var suffix = 2;
        while (!usedNames.Add($"{sanitized}_{suffix}"))
        {
            suffix++;
        }

        return $"{sanitized}_{suffix}";
    }
}
