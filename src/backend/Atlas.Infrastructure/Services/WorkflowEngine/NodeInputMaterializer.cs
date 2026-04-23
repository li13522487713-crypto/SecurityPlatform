using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Domain.AiPlatform.ValueObjects;

namespace Atlas.Infrastructure.Services.WorkflowEngine;

/// <summary>
/// 基于节点声明与运行变量，为节点构建执行上下文与输入快照。
/// </summary>
internal static partial class NodeInputMaterializer
{
    private static readonly HashSet<string> VariablePathConfigKeys =
    [
        "inputPath",
        "answerPath",
        "collectionPath",
        "inputArrayPath",
        "inputsVariable",
        "inputVariable"
    ];

    public static NodeInputMaterializationResult Materialize(
        NodeSchema node,
        IReadOnlyDictionary<string, JsonElement> runtimeVariables)
    {
        var preparedVariables = new Dictionary<string, JsonElement>(runtimeVariables, StringComparer.OrdinalIgnoreCase);
        var snapshot = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["__config"] = JsonSerializer.SerializeToElement(node.Config)
        };

        var hasExplicitMappings = false;
        foreach (var mapping in EnumerateMappings(node))
        {
            if (TryResolveMappingValue(runtimeVariables, mapping.Path, out var resolved))
            {
                preparedVariables[mapping.Field] = resolved.Clone();
                snapshot[mapping.Field] = resolved.Clone();
                hasExplicitMappings = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(mapping.DefaultValue))
            {
                continue;
            }

            var defaultValue = VariableResolver.ParseLiteralOrTemplate(mapping.DefaultValue, runtimeVariables);
            preparedVariables[mapping.Field] = defaultValue;
            snapshot[mapping.Field] = defaultValue;
            hasExplicitMappings = true;
        }

        if (hasExplicitMappings)
        {
            return new NodeInputMaterializationResult(preparedVariables, snapshot);
        }

        ProcessCozeInputs(node.Config, runtimeVariables, preparedVariables, snapshot, ref hasExplicitMappings);

        foreach (var path in ExtractReferencedVariablePaths(node))
        {
            if (!TryResolveMappingValue(runtimeVariables, path, out var resolved))
            {
                continue;
            }

            snapshot[path] = resolved.Clone();
        }

        if (snapshot.Count == 1)
        {
            foreach (var variable in runtimeVariables)
            {
                snapshot[variable.Key] = variable.Value.Clone();
            }
        }

        return new NodeInputMaterializationResult(preparedVariables, snapshot);
    }

    private static IEnumerable<NodeFieldMapping> EnumerateMappings(NodeSchema node)
    {
        var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (node.InputSources is not null)
        {
            foreach (var mapping in node.InputSources)
            {
                if (string.IsNullOrWhiteSpace(mapping.Field) || string.IsNullOrWhiteSpace(mapping.Path))
                {
                    continue;
                }

                if (!seenFields.Add(mapping.Field))
                {
                    continue;
                }

                yield return mapping;
            }
        }

        if (!node.Config.TryGetValue("inputMappings", out var rawMappings) ||
            rawMappings.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in rawMappings.EnumerateObject())
        {
            var field = property.Name.Trim();
            var path = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()?.Trim()
                : null;
            if (string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (!seenFields.Add(field))
            {
                continue;
            }

            yield return new NodeFieldMapping(field, path);
        }
    }

    private static IEnumerable<string> ExtractReferencedVariablePaths(NodeSchema node)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in node.Config)
        {
            ExtractReferencedVariablePaths(entry.Key, entry.Value, paths);
        }

        return paths;
    }

    private static void ExtractReferencedVariablePaths(
        string key,
        JsonElement value,
        ISet<string> paths)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    ExtractReferencedVariablePaths(property.Name, property.Value, paths);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    ExtractReferencedVariablePaths(key, item, paths);
                }

                break;
            case JsonValueKind.String:
            {
                var text = value.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    break;
                }

                foreach (Match match in PlaceholderRegex().Matches(text))
                {
                    var normalized = NormalizePath(match.Groups["path"].Value);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        paths.Add(normalized);
                    }
                }

                if (VariablePathConfigKeys.Contains(key))
                {
                    var normalized = NormalizePath(text);
                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        paths.Add(normalized);
                    }
                }

                break;
            }
        }
        
        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("type", out var typeProp) &&
            typeProp.ValueKind == JsonValueKind.String &&
            string.Equals(typeProp.GetString(), "ref", StringComparison.OrdinalIgnoreCase) &&
            value.TryGetProperty("content", out var contentProp) &&
            contentProp.ValueKind == JsonValueKind.Object &&
            contentProp.TryGetProperty("keyPath", out var keyPathProp) &&
            keyPathProp.ValueKind == JsonValueKind.Array)
        {
            var pathSegments = keyPathProp.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x));
            var refPath = string.Join(".", pathSegments);
            if (!string.IsNullOrWhiteSpace(refPath))
            {
                paths.Add(NormalizePath(refPath));
            }
        }
    }

    private static bool TryResolveMappingValue(
        IReadOnlyDictionary<string, JsonElement> runtimeVariables,
        string path,
        out JsonElement value)
    {
        var normalized = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            value = default;
            return false;
        }

        if (runtimeVariables.TryGetValue(normalized, out value))
        {
            return true;
        }

        if (TryResolveNodeFieldAlias(runtimeVariables, normalized, out value))
        {
            return true;
        }

        // Coze 的 block-output 常见形式：{blockID}.{name} 或 block_output_{blockID}.{name}
        // Atlas 运行态变量优先按字段名扁平存储，这里做兼容降级，避免历史画布字段丢失。
        if (TryResolveCozeBlockOutputAlias(runtimeVariables, normalized, out value))
        {
            return true;
        }

        return VariableResolver.TryResolvePath(runtimeVariables, normalized, out value);
    }

    private static string NormalizePath(string? rawPath)
    {
        var value = rawPath?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (value.StartsWith("{{", StringComparison.Ordinal) &&
            value.EndsWith("}}", StringComparison.Ordinal) &&
            value.Length > 4)
        {
            value = value[2..^2].Trim();
        }

        if (value.StartsWith("global.", StringComparison.OrdinalIgnoreCase))
        {
            value = value["global.".Length..];
        }

        if (value.StartsWith('$'))
        {
            value = value.TrimStart('$');
        }

        return value.Trim();
    }

    private static void ProcessCozeInputs(
        IReadOnlyDictionary<string, JsonElement> config,
        IReadOnlyDictionary<string, JsonElement> runtimeVariables,
        Dictionary<string, JsonElement> preparedVariables,
        Dictionary<string, JsonElement> snapshot,
        ref bool hasMappings)
    {
        if (config.TryGetValue("inputs", out var inputsRaw) && inputsRaw.ValueKind == JsonValueKind.Object)
        {
            ProcessCozeValueExpressions(inputsRaw, runtimeVariables, preparedVariables, snapshot, ref hasMappings);
        }
    }

    private static void ProcessCozeValueExpressions(
        JsonElement element,
        IReadOnlyDictionary<string, JsonElement> runtimeVariables,
        Dictionary<string, JsonElement> preparedVariables,
        Dictionary<string, JsonElement> snapshot,
        ref bool hasMappings)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ProcessCozeValueExpressions(item, runtimeVariables, preparedVariables, snapshot, ref hasMappings);
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("name", out var nameProp) &&
                nameProp.ValueKind == JsonValueKind.String &&
                element.TryGetProperty("input", out var inputProp) &&
                inputProp.ValueKind == JsonValueKind.Object &&
                TryGetCozeValueExpression(inputProp, out var valueType, out var contentProp))
            {
                var name = nameProp.GetString()?.Trim();
                var type = valueType.Trim().ToLowerInvariant();

                if (!string.IsNullOrWhiteSpace(name))
                {
                    JsonElement resolvedValue = default;
                    bool resolved = false;

                    if (type == "ref" && contentProp.ValueKind == JsonValueKind.Object)
                    {
                        if (TryResolveRefPathFromContent(contentProp, out var refPath))
                        {
                            resolved = TryResolveMappingValue(runtimeVariables, refPath, out resolvedValue);
                        }
                    }
                    else if (type == "literal" || type == "object_ref")
                    {
                        if (contentProp.ValueKind == JsonValueKind.String)
                        {
                            resolvedValue = VariableResolver.ParseLiteralOrTemplate(contentProp.GetString() ?? string.Empty, runtimeVariables);
                        }
                        else if (contentProp.ValueKind != JsonValueKind.Undefined && contentProp.ValueKind != JsonValueKind.Null)
                        {
                            resolvedValue = contentProp.Clone();
                        }
                        resolved = true;
                    }

                    if (resolved)
                    {
                        preparedVariables[name] = resolvedValue.Clone();
                        snapshot[name] = resolvedValue.Clone();
                        hasMappings = true;
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                ProcessCozeValueExpressions(property.Value, runtimeVariables, preparedVariables, snapshot, ref hasMappings);
            }
        }
    }

    private static bool TryGetCozeValueExpression(
        JsonElement inputElement,
        out string valueType,
        out JsonElement contentElement)
    {
        valueType = string.Empty;
        contentElement = default;

        // 新版结构：input.value.{type,content}
        if (inputElement.TryGetProperty("value", out var valueElement) &&
            valueElement.ValueKind == JsonValueKind.Object &&
            valueElement.TryGetProperty("type", out var nestedTypeProp) &&
            nestedTypeProp.ValueKind == JsonValueKind.String &&
            valueElement.TryGetProperty("content", out var nestedContent))
        {
            valueType = nestedTypeProp.GetString() ?? string.Empty;
            contentElement = nestedContent;
            return !string.IsNullOrWhiteSpace(valueType);
        }

        // 兼容旧结构：input.{type,content}
        if (inputElement.TryGetProperty("type", out var typeProp) &&
            typeProp.ValueKind == JsonValueKind.String &&
            inputElement.TryGetProperty("content", out var contentProp))
        {
            valueType = typeProp.GetString() ?? string.Empty;
            contentElement = contentProp;
            return !string.IsNullOrWhiteSpace(valueType);
        }

        return false;
    }

    private static bool TryResolveRefPathFromContent(JsonElement contentElement, out string refPath)
    {
        refPath = string.Empty;
        if (contentElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (contentElement.TryGetProperty("keyPath", out var keyPathProp) &&
            keyPathProp.ValueKind == JsonValueKind.Array)
        {
            var pathSegments = keyPathProp.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x));
            var joined = string.Join(".", pathSegments);
            if (!string.IsNullOrWhiteSpace(joined))
            {
                refPath = joined;
                return true;
            }
        }

        // Coze 原生结构：content.{blockID,name,source}
        if (contentElement.TryGetProperty("blockID", out var blockIdProp) &&
            blockIdProp.ValueKind == JsonValueKind.String &&
            contentElement.TryGetProperty("name", out var nameProp) &&
            nameProp.ValueKind == JsonValueKind.String)
        {
            var blockId = blockIdProp.GetString()?.Trim();
            var name = nameProp.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(blockId) && !string.IsNullOrWhiteSpace(name))
            {
                refPath = $"{blockId}.{name}";
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveCozeBlockOutputAlias(
        IReadOnlyDictionary<string, JsonElement> runtimeVariables,
        string normalizedPath,
        out JsonElement value)
    {
        value = default;
        var separatorIndex = normalizedPath.IndexOf('.');
        if (separatorIndex <= 0 || separatorIndex >= normalizedPath.Length - 1)
        {
            return false;
        }

        var blockSegment = normalizedPath[..separatorIndex];
        var fieldSegment = normalizedPath[(separatorIndex + 1)..];
        if (string.IsNullOrWhiteSpace(fieldSegment))
        {
            return false;
        }

        if (runtimeVariables.TryGetValue(fieldSegment, out value))
        {
            return true;
        }

        if (blockSegment.StartsWith("block_output_", StringComparison.OrdinalIgnoreCase))
        {
            var blockId = blockSegment["block_output_".Length..];
            if (!string.IsNullOrWhiteSpace(blockId))
            {
                var candidate = $"{blockId}.{fieldSegment}";
                if (runtimeVariables.TryGetValue(candidate, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryResolveNodeFieldAlias(
        IReadOnlyDictionary<string, JsonElement> runtimeVariables,
        string normalizedPath,
        out JsonElement value)
    {
        value = default;
        var dotIndex = normalizedPath.IndexOf('.');
        if (dotIndex <= 0 || dotIndex >= normalizedPath.Length - 1)
        {
            return false;
        }

        var field = normalizedPath[(dotIndex + 1)..];
        if (runtimeVariables.TryGetValue(field, out value))
        {
            return true;
        }

        const string outputsMarker = ".outputs.";
        var markerIndex = normalizedPath.IndexOf(outputsMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex > 0 && markerIndex + outputsMarker.Length < normalizedPath.Length)
        {
            var outputField = normalizedPath[(markerIndex + outputsMarker.Length)..];
            if (runtimeVariables.TryGetValue(outputField, out value))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"\{\{\s*(?<path>[^{}]+?)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();
}

internal sealed record NodeInputMaterializationResult(
    Dictionary<string, JsonElement> PreparedVariables,
    Dictionary<string, JsonElement> Snapshot);
