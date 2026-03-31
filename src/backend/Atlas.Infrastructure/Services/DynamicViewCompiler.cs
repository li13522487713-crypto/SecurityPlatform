using Atlas.Application.DynamicViews.Models;
using System.Text.Json;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicViewCompiler
{
    public DynamicViewExecutionPlan Compile(DynamicViewCreateOrUpdateRequest definition)
    {
        var sourceNode = definition.Nodes.FirstOrDefault(node => string.Equals(node.Type, "sourceTable", StringComparison.OrdinalIgnoreCase));
        if (sourceNode is null || string.IsNullOrWhiteSpace(sourceNode.TableKey))
        {
            throw new InvalidOperationException("Dynamic view requires at least one sourceTable node with tableKey.");
        }

        var mappings = definition.OutputFields
            .Where(field => field.Source is not null && !string.IsNullOrWhiteSpace(field.Source.FieldKey))
            .Select(field => new DynamicViewFieldPlan(
                field.TargetFieldKey,
                field.TargetLabel,
                field.TargetType,
                field.Source!.FieldKey,
                field.Pipeline,
                field.OnError))
            .ToArray();

        return new DynamicViewExecutionPlan(
            sourceNode.TableKey!,
            mappings,
            definition.Filters ?? Array.Empty<JsonFilterRuleDto>(),
            definition.Sorts ?? Array.Empty<DynamicViewSortDto>(),
            ResolveLimit(definition.Nodes));
    }

    private static int? ResolveLimit(IReadOnlyList<DynamicViewNodeDto> nodes)
    {
        var limitNode = nodes.FirstOrDefault(node => string.Equals(node.Type, "limit", StringComparison.OrdinalIgnoreCase));
        if (limitNode?.Config is null)
        {
            return null;
        }

        if (limitNode.Config is JsonElement json)
        {
            if (TryReadLimitFromJson(json, out var fromJson))
            {
                return fromJson;
            }
        }

        try
        {
            var raw = JsonSerializer.Serialize(limitNode.Config);
            using var doc = JsonDocument.Parse(raw);
            if (TryReadLimitFromJson(doc.RootElement, out var parsed))
            {
                return parsed;
            }
        }
        catch
        {
            // ignore invalid config
        }

        return null;
    }

    private static bool TryReadLimitFromJson(JsonElement element, out int? limit)
    {
        limit = null;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetIntProperty(element, "limit", out var value)
            && !TryGetIntProperty(element, "size", out value)
            && !TryGetIntProperty(element, "count", out value))
        {
            return false;
        }

        limit = value > 0 ? value : null;
        return true;
    }

    private static bool TryGetIntProperty(JsonElement element, string name, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(name, out var prop))
        {
            return false;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out value))
        {
            return true;
        }

        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out value))
        {
            return true;
        }

        return false;
    }
}

public sealed record DynamicViewExecutionPlan(
    string SourceTableKey,
    IReadOnlyList<DynamicViewFieldPlan> Fields,
    IReadOnlyList<JsonFilterRuleDto> Filters,
    IReadOnlyList<DynamicViewSortDto> Sorts,
    int? Limit);

public sealed record DynamicViewFieldPlan(
    string TargetFieldKey,
    string TargetLabel,
    string TargetType,
    string SourceFieldKey,
    IReadOnlyList<DynamicViewTransformOpDto> Pipeline,
    string? OnError);
