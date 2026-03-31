using System.Text;
using System.Text.Json;
using Atlas.Application.DynamicViews.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;

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

        var joinPlans = BuildJoinPlans(definition.Nodes);
        var aggregatePlan = BuildAggregatePlan(definition.Nodes, definition.GroupBy);
        var unionPlan = BuildUnionPlan(definition.Nodes, sourceNode.TableKey!);

        return new DynamicViewExecutionPlan(
            sourceNode.TableKey!,
            mappings,
            definition.Filters ?? Array.Empty<JsonFilterRuleDto>(),
            definition.Sorts ?? Array.Empty<DynamicViewSortDto>(),
            ResolveLimit(definition.Nodes),
            joinPlans,
            aggregatePlan,
            unionPlan);
    }

    public DynamicViewSqlPreviewDto BuildSqlPreview(DynamicViewCreateOrUpdateRequest definition)
    {
        var plan = Compile(definition);
        var warnings = new List<string>();
        var fullyPushdown = true;

        var sql = new StringBuilder();
        sql.Append("SELECT ");
        if (plan.Fields.Count == 0)
        {
            sql.Append("*");
        }
        else
        {
            sql.Append(string.Join(", ", plan.Fields.Select(field => $"{QuoteIdentifier(field.SourceFieldKey)} AS {QuoteIdentifier(field.TargetFieldKey)}")));
        }

        sql.AppendLine();
        sql.Append($"FROM {QuoteIdentifier(plan.SourceTableKey)} src");

        foreach (var join in plan.JoinPlans)
        {
            var joinType = join.JoinType.ToUpperInvariant() switch
            {
                "INNER" => "INNER JOIN",
                "LEFT" => "LEFT JOIN",
                "RIGHT" => "RIGHT JOIN",
                "FULL" => "FULL OUTER JOIN",
                _ => "INNER JOIN"
            };
            if (joinType.StartsWith("RIGHT", StringComparison.Ordinal) || joinType.StartsWith("FULL", StringComparison.Ordinal))
            {
                fullyPushdown = false;
                warnings.Add($"Join {join.JoinType} 依赖运行时补偿（SQLite 不完全支持）。");
            }

            var onClause = join.Conditions.Count == 0
                ? "1=1"
                : string.Join(" AND ", join.Conditions.Select(c => $"src.{QuoteIdentifier(c.LeftField)} = {QuoteIdentifier(join.RightSource)}.{QuoteIdentifier(c.RightField)}"));
            sql.AppendLine();
            sql.Append($"{joinType} {QuoteIdentifier(join.RightSource)} ON {onClause}");
        }

        if (plan.Filters.Count > 0)
        {
            sql.AppendLine();
            sql.Append("WHERE ");
            sql.Append(string.Join(" AND ", plan.Filters.Select(ToSqlFilter)));
        }

        if (plan.AggregatePlan is not null)
        {
            if (plan.AggregatePlan.GroupBy.Count > 0)
            {
                sql.AppendLine();
                sql.Append("GROUP BY ");
                sql.Append(string.Join(", ", plan.AggregatePlan.GroupBy.Select(QuoteIdentifier)));
            }
        }

        if (plan.Sorts.Count > 0)
        {
            sql.AppendLine();
            sql.Append("ORDER BY ");
            sql.Append(string.Join(", ", plan.Sorts.Select(sort => $"{QuoteIdentifier(sort.Field)} {(string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC")}")));
        }

        if (plan.Limit is > 0)
        {
            sql.AppendLine();
            sql.Append($"LIMIT {plan.Limit.Value}");
        }

        if (plan.UnionPlan is not null && plan.UnionPlan.Inputs.Count > 1)
        {
            fullyPushdown = false;
            warnings.Add("Union 采用运行时对齐（byName/byPosition），SQL 预览为逻辑结果 SQL。");
        }

        return new DynamicViewSqlPreviewDto(
            sql.ToString(),
            Array.Empty<DynamicSqlParameterDto>(),
            warnings,
            fullyPushdown);
    }

    private static IReadOnlyList<DynamicJoinPlanDto> BuildJoinPlans(IReadOnlyList<DynamicViewNodeDto> nodes)
    {
        var plans = new List<DynamicJoinPlanDto>();
        foreach (var node in nodes.Where(node => string.Equals(node.Type, "join", StringComparison.OrdinalIgnoreCase)))
        {
            var json = TryNormalizeConfig(node.Config);
            var joinType = ReadString(json, "joinType", "inner");
            var leftSource = ReadString(json, "leftSource", "source");
            var rightSource = ReadString(json, "rightSource", ReadString(json, "rightTable", string.Empty));
            if (string.IsNullOrWhiteSpace(rightSource))
            {
                continue;
            }

            var conditions = new List<DynamicJoinConditionDto>();
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("conditions", out var conds) && conds.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in conds.EnumerateArray())
                {
                    var left = ReadString(c, "leftField", string.Empty);
                    var right = ReadString(c, "rightField", string.Empty);
                    if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
                    {
                        conditions.Add(new DynamicJoinConditionDto(left, right));
                    }
                }
            }

            plans.Add(new DynamicJoinPlanDto(joinType, leftSource, rightSource, conditions));
        }

        return plans;
    }

    private static DynamicAggregatePlanDto? BuildAggregatePlan(IReadOnlyList<DynamicViewNodeDto> nodes, IReadOnlyList<string>? definitionGroupBy)
    {
        var aggregateNode = nodes.FirstOrDefault(node => string.Equals(node.Type, "aggregate", StringComparison.OrdinalIgnoreCase));
        if (aggregateNode is null)
        {
            return null;
        }

        var json = TryNormalizeConfig(aggregateNode.Config);
        var groupBy = new List<string>();
        var aggregates = new List<DynamicAggregateItemDto>();

        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("groupBy", out var groupByJson) && groupByJson.ValueKind == JsonValueKind.Array)
        {
            groupBy.AddRange(groupByJson.EnumerateArray()
                .Select(item => item.GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!));
        }

        if (definitionGroupBy is not null)
        {
            var left = new HashSet<string>(groupBy, StringComparer.OrdinalIgnoreCase);
            var right = new HashSet<string>(definitionGroupBy, StringComparer.OrdinalIgnoreCase);
            if (!left.SetEquals(right))
            {
                throw new BusinessException(ErrorCodes.ValidationError, "DynamicViewAggregateGroupByMismatch");
            }
        }

        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("aggregates", out var aggJson) && aggJson.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in aggJson.EnumerateArray())
            {
                var function = ReadString(item, "function", "count");
                var field = ReadString(item, "field", string.Empty);
                var alias = ReadString(item, "alias", $"{function}_{field}");
                aggregates.Add(new DynamicAggregateItemDto(function, string.IsNullOrWhiteSpace(field) ? null : field, alias));
            }
        }

        return new DynamicAggregatePlanDto(groupBy, aggregates);
    }

    private static DynamicUnionPlanDto? BuildUnionPlan(IReadOnlyList<DynamicViewNodeDto> nodes, string sourceTable)
    {
        var unionNode = nodes.FirstOrDefault(node => string.Equals(node.Type, "union", StringComparison.OrdinalIgnoreCase));
        if (unionNode is null)
        {
            return null;
        }

        var json = TryNormalizeConfig(unionNode.Config);
        var mode = ReadString(json, "mode", "byName");
        var inputs = new List<string> { sourceTable };

        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("inputs", out var inputJson) && inputJson.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in inputJson.EnumerateArray())
            {
                var key = item.GetString();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    inputs.Add(key);
                }
            }
        }

        return new DynamicUnionPlanDto(mode, inputs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static int? ResolveLimit(IReadOnlyList<DynamicViewNodeDto> nodes)
    {
        var limitNode = nodes.FirstOrDefault(node => string.Equals(node.Type, "limit", StringComparison.OrdinalIgnoreCase));
        if (limitNode?.Config is null)
        {
            return null;
        }

        var json = TryNormalizeConfig(limitNode.Config);
        if (TryGetIntProperty(json, "limit", out var value) || TryGetIntProperty(json, "size", out value) || TryGetIntProperty(json, "count", out value))
        {
            return value > 0 ? value : null;
        }

        return null;
    }

    private static JsonElement TryNormalizeConfig(object? config)
    {
        if (config is JsonElement element)
        {
            return element;
        }

        try
        {
            var raw = JsonSerializer.Serialize(config ?? new { });
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Clone();
        }
        catch
        {
            using var fallback = JsonDocument.Parse("{}");
            return fallback.RootElement.Clone();
        }
    }

    private static bool TryGetIntProperty(JsonElement element, string name, out int value)
    {
        value = 0;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var prop))
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

    private static string ReadString(JsonElement element, string name, string defaultValue)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var prop))
        {
            return defaultValue;
        }

        if (prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString() ?? defaultValue;
        }

        return defaultValue;
    }

    private static string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string ToSqlFilter(JsonFilterRuleDto filter)
    {
        var field = QuoteIdentifier(filter.Field);
        var value = filter.Value switch
        {
            null => "NULL",
            string s => $"'{s.Replace("'", "''", StringComparison.Ordinal)}'",
            JsonElement j when j.ValueKind == JsonValueKind.String => $"'{(j.GetString() ?? string.Empty).Replace("'", "''", StringComparison.Ordinal)}'",
            JsonElement j => j.ToString(),
            _ => filter.Value.ToString() ?? "NULL"
        };
        var op = (filter.Operator ?? "eq").ToLowerInvariant();
        return op switch
        {
            "eq" => $"{field} = {value}",
            "ne" => $"{field} <> {value}",
            "contains" => $"{field} LIKE '%' || {value} || '%'",
            "startswith" => $"{field} LIKE {value} || '%'",
            "endswith" => $"{field} LIKE '%' || {value}",
            "gt" => $"{field} > {value}",
            "ge" => $"{field} >= {value}",
            "lt" => $"{field} < {value}",
            "le" => $"{field} <= {value}",
            _ => "1=1"
        };
    }
}

public sealed record DynamicViewExecutionPlan(
    string SourceTableKey,
    IReadOnlyList<DynamicViewFieldPlan> Fields,
    IReadOnlyList<JsonFilterRuleDto> Filters,
    IReadOnlyList<DynamicViewSortDto> Sorts,
    int? Limit,
    IReadOnlyList<DynamicJoinPlanDto> JoinPlans,
    DynamicAggregatePlanDto? AggregatePlan,
    DynamicUnionPlanDto? UnionPlan);

public sealed record DynamicViewFieldPlan(
    string TargetFieldKey,
    string TargetLabel,
    string TargetType,
    string SourceFieldKey,
    IReadOnlyList<DynamicViewTransformOpDto> Pipeline,
    string? OnError);
