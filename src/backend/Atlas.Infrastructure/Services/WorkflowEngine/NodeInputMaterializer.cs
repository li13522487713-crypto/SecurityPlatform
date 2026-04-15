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

        return value.Trim();
    }

    [GeneratedRegex(@"\{\{\s*(?<path>[^{}]+?)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();
}

internal sealed record NodeInputMaterializationResult(
    Dictionary<string, JsonElement> PreparedVariables,
    Dictionary<string, JsonElement> Snapshot);
