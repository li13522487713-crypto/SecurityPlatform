using System.Globalization;
using System.Text.Json;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Application.DynamicViews.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicViewRuntime
{
    private readonly IDynamicTableRepository _tableRepository;
    private readonly IDynamicFieldRepository _fieldRepository;
    private readonly IDynamicRecordRepository _recordRepository;
    private readonly IAppContextAccessor _appContextAccessor;

    public DynamicViewRuntime(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicRecordRepository recordRepository,
        IAppContextAccessor appContextAccessor)
    {
        _tableRepository = tableRepository;
        _fieldRepository = fieldRepository;
        _recordRepository = recordRepository;
        _appContextAccessor = appContextAccessor;
    }

    public async Task<DynamicRecordListResult> ExecuteAsync(
        TenantId tenantId,
        long? appId,
        DynamicViewExecutionPlan plan,
        DynamicViewRecordsQueryRequest request,
        CancellationToken cancellationToken)
    {
        var effectiveAppId = appId ?? _appContextAccessor.ResolveAppId();
        var sourceResult = await QueryTableRowsAsync(tenantId, effectiveAppId, plan.SourceTableKey, request, cancellationToken);

        var items = sourceResult.Items.ToList();
        if (plan.JoinPlans.Count > 0)
        {
            items = await ExecuteJoinsAsync(tenantId, effectiveAppId, items, plan.JoinPlans, cancellationToken);
        }

        if (plan.UnionPlan is not null && plan.UnionPlan.Inputs.Count > 1)
        {
            items = await ExecuteUnionAsync(tenantId, effectiveAppId, plan.UnionPlan, request, items, cancellationToken);
        }

        var projected = plan.Fields.Count > 0 ? ProjectItems(items, plan.Fields) : items;
        if (plan.AggregatePlan is not null)
        {
            projected = ExecuteAggregate(projected, plan.AggregatePlan);
        }

        var resultColumns = BuildColumns(projected, plan.Fields);
        var projectedResult = new DynamicRecordListResult(projected, projected.Count, request.PageIndex, request.PageSize, resultColumns);
        return ApplyViewPostProcessing(projectedResult, plan);
    }

    private async Task<DynamicRecordListResult> QueryTableRowsAsync(
        TenantId tenantId,
        long? appId,
        string tableKey,
        DynamicViewRecordsQueryRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, appId, cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTableNotFound");
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var queryRequest = new DynamicRecordQueryRequest(
            1,
            Math.Max(1000, request.PageSize),
            request.Keyword,
            request.SortBy,
            request.SortDesc,
            request.Filters)
        {
            AdvancedQuery = request.AdvancedQuery
        };

        var result = await _recordRepository.QueryAsync(tenantId, table, fields, queryRequest, cancellationToken);
        return new DynamicRecordListResult(
            result.Items.Select(item => NormalizeRecord(item, tableKey)).ToArray(),
            result.Total,
            result.PageIndex,
            result.PageSize,
            result.Columns);
    }

    private static DynamicRecordDto NormalizeRecord(DynamicRecordDto record, string tableKey)
    {
        var values = new List<DynamicFieldValueDto>();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in record.Values)
        {
            values.Add(value);
            existing.Add(value.Field);

            var qualified = $"{tableKey}.{value.Field}";
            if (existing.Add(qualified))
            {
                values.Add(value with { Field = qualified });
            }
        }

        return new DynamicRecordDto(record.Id, values);
    }

    private async Task<List<DynamicRecordDto>> ExecuteJoinsAsync(
        TenantId tenantId,
        long? appId,
        List<DynamicRecordDto> leftRows,
        IReadOnlyList<DynamicJoinPlanDto> joins,
        CancellationToken cancellationToken)
    {
        var current = leftRows;
        foreach (var join in joins)
        {
            var rightResult = await QueryTableRowsAsync(
                tenantId,
                appId,
                join.RightSource,
                new DynamicViewRecordsQueryRequest(1, 1000, null, null, false, null),
                cancellationToken);

            current = ApplyJoin(current, rightResult.Items.ToList(), join);
        }

        return current;
    }

    private static List<DynamicRecordDto> ApplyJoin(List<DynamicRecordDto> left, List<DynamicRecordDto> right, DynamicJoinPlanDto plan)
    {
        var joinType = (plan.JoinType ?? "inner").ToLowerInvariant();
        var results = new List<DynamicRecordDto>();
        var matchedRight = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var leftRow in left)
        {
            var matches = right.Where(rightRow => MatchJoin(leftRow, rightRow, plan.Conditions)).ToList();
            if (matches.Count == 0)
            {
                if (joinType is "left" or "full")
                {
                    results.Add(leftRow);
                }
                continue;
            }

            foreach (var match in matches)
            {
                matchedRight.Add(match.Id);
                results.Add(MergeRows(leftRow, match));
            }
        }

        if (joinType is "right" or "full")
        {
            var leftFieldTemplate = left.FirstOrDefault()?.Values ?? Array.Empty<DynamicFieldValueDto>();
            foreach (var rightRow in right.Where(row => !matchedRight.Contains(row.Id)))
            {
                results.Add(MergeRows(BuildNullLeftRow(leftFieldTemplate, rightRow.Id), rightRow));
            }
        }

        return results;
    }

    private static DynamicRecordDto BuildNullLeftRow(IReadOnlyList<DynamicFieldValueDto> template, string id)
    {
        return new DynamicRecordDto(
            $"left-null-{id}",
            template.Select(field => new DynamicFieldValueDto { Field = field.Field, ValueType = field.ValueType }).ToArray());
    }

    private static bool MatchJoin(DynamicRecordDto left, DynamicRecordDto right, IReadOnlyList<DynamicJoinConditionDto> conditions)
    {
        if (conditions.Count == 0)
        {
            return true;
        }

        foreach (var condition in conditions)
        {
            var lv = ReadAsString(left.Values.FirstOrDefault(v => string.Equals(v.Field, condition.LeftField, StringComparison.OrdinalIgnoreCase)));
            var rv = ReadAsString(right.Values.FirstOrDefault(v => string.Equals(v.Field, condition.RightField, StringComparison.OrdinalIgnoreCase)));
            if (!string.Equals(lv, rv, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static DynamicRecordDto MergeRows(DynamicRecordDto left, DynamicRecordDto right)
    {
        var values = new List<DynamicFieldValueDto>();
        values.AddRange(left.Values);
        foreach (var value in right.Values)
        {
            if (values.All(v => !string.Equals(v.Field, value.Field, StringComparison.OrdinalIgnoreCase)))
            {
                values.Add(value);
            }
        }

        return new DynamicRecordDto($"{left.Id}|{right.Id}", values);
    }

    private async Task<List<DynamicRecordDto>> ExecuteUnionAsync(
        TenantId tenantId,
        long? appId,
        DynamicUnionPlanDto unionPlan,
        DynamicViewRecordsQueryRequest request,
        List<DynamicRecordDto> current,
        CancellationToken cancellationToken)
    {
        var inputs = new List<List<DynamicRecordDto>> { current };
        foreach (var table in unionPlan.Inputs.Skip(1))
        {
            var result = await QueryTableRowsAsync(tenantId, appId, table, request, cancellationToken);
            inputs.Add(result.Items.ToList());
        }

        return string.Equals(unionPlan.Mode, "byPosition", StringComparison.OrdinalIgnoreCase)
            ? UnionByPosition(inputs)
            : UnionByName(inputs);
    }

    private static List<DynamicRecordDto> UnionByName(List<List<DynamicRecordDto>> sets)
    {
        var allFields = sets.SelectMany(set => set.SelectMany(row => row.Values.Select(v => v.Field))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var rows = new List<DynamicRecordDto>();
        foreach (var set in sets)
        {
            foreach (var row in set)
            {
                var values = allFields.Select(field => row.Values.FirstOrDefault(v => string.Equals(v.Field, field, StringComparison.OrdinalIgnoreCase))
                    ?? new DynamicFieldValueDto { Field = field, ValueType = "String" }).ToArray();
                rows.Add(new DynamicRecordDto(row.Id, values));
            }
        }

        return rows;
    }

    private static List<DynamicRecordDto> UnionByPosition(List<List<DynamicRecordDto>> sets)
    {
        var template = sets.SelectMany(set => set).FirstOrDefault()?.Values.Select(v => v.Field).ToArray() ?? Array.Empty<string>();
        var rows = new List<DynamicRecordDto>();
        foreach (var set in sets)
        {
            foreach (var row in set)
            {
                var ordered = row.Values.ToArray();
                var mapped = new List<DynamicFieldValueDto>();
                for (var i = 0; i < template.Length; i++)
                {
                    if (i < ordered.Length)
                    {
                        mapped.Add(ordered[i] with { Field = template[i] });
                    }
                    else
                    {
                        mapped.Add(new DynamicFieldValueDto { Field = template[i], ValueType = "String" });
                    }
                }

                rows.Add(new DynamicRecordDto(row.Id, mapped));
            }
        }

        return rows;
    }

    private static List<DynamicRecordDto> ExecuteAggregate(List<DynamicRecordDto> items, DynamicAggregatePlanDto plan)
    {
        if (plan.GroupBy.Count == 0)
        {
            return items;
        }

        var groups = items.GroupBy(item => string.Join("||", plan.GroupBy.Select(field => ReadField(item, field))));
        var result = new List<DynamicRecordDto>();
        foreach (var group in groups)
        {
            var values = new List<DynamicFieldValueDto>();
            var first = group.First();
            foreach (var groupKey in plan.GroupBy)
            {
                var field = first.Values.FirstOrDefault(v => string.Equals(v.Field, groupKey, StringComparison.OrdinalIgnoreCase));
                if (field is not null)
                {
                    values.Add(field);
                }
            }

            foreach (var aggregate in plan.Aggregates)
            {
                values.Add(ComputeAggregate(group.ToList(), aggregate));
            }

            result.Add(new DynamicRecordDto(group.Key, values));
        }

        return result;
    }

    private static DynamicFieldValueDto ComputeAggregate(List<DynamicRecordDto> group, DynamicAggregateItemDto aggregate)
    {
        var fn = aggregate.Function.ToLowerInvariant();
        var alias = string.IsNullOrWhiteSpace(aggregate.Alias) ? $"{aggregate.Function}_{aggregate.Field}" : aggregate.Alias;
        var values = group
            .Select(row => row.Values.FirstOrDefault(v => string.Equals(v.Field, aggregate.Field, StringComparison.OrdinalIgnoreCase)))
            .Where(x => x is not null)
            .Select(ReadAsDecimal)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        return fn switch
        {
            "count" => new DynamicFieldValueDto { Field = alias, ValueType = "Long", LongValue = group.Count },
            "sum" => new DynamicFieldValueDto { Field = alias, ValueType = "Decimal", DecimalValue = values.Sum() },
            "avg" => new DynamicFieldValueDto { Field = alias, ValueType = "Decimal", DecimalValue = values.Count == 0 ? 0 : values.Average() },
            "min" => new DynamicFieldValueDto { Field = alias, ValueType = "Decimal", DecimalValue = values.Count == 0 ? 0 : values.Min() },
            "max" => new DynamicFieldValueDto { Field = alias, ValueType = "Decimal", DecimalValue = values.Count == 0 ? 0 : values.Max() },
            _ => new DynamicFieldValueDto { Field = alias, ValueType = "Long", LongValue = group.Count }
        };
    }

    private static decimal? ReadAsDecimal(DynamicFieldValueDto? value)
    {
        if (value is null)
        {
            return null;
        }
        return value.ValueType switch
        {
            "Int" => value.IntValue,
            "Long" => value.LongValue,
            "Decimal" => value.DecimalValue,
            "String" or "Text" => decimal.TryParse(value.StringValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null,
            _ => null
        };
    }

    private static string ReadField(DynamicRecordDto row, string field)
    {
        return ReadAsString(row.Values.FirstOrDefault(v => string.Equals(v.Field, field, StringComparison.OrdinalIgnoreCase))) ?? string.Empty;
    }

    private static List<DynamicRecordDto> ProjectItems(List<DynamicRecordDto> items, IReadOnlyList<DynamicViewFieldPlan> mappings)
    {
        return items.Select(item => new DynamicRecordDto(item.Id, mappings.Select(mapping => ProjectField(item, mapping)).ToArray())).ToList();
    }

    private static DynamicFieldValueDto ProjectField(DynamicRecordDto item, DynamicViewFieldPlan mapping)
    {
        var sourceValue = item.Values.FirstOrDefault(value => string.Equals(value.Field, mapping.SourceFieldKey, StringComparison.OrdinalIgnoreCase));
        if (sourceValue is null)
        {
            return new DynamicFieldValueDto
            {
                Field = mapping.TargetFieldKey,
                ValueType = NormalizeValueType(mapping.TargetType)
            };
        }

        var current = sourceValue with
        {
            Field = mapping.TargetFieldKey,
            ValueType = NormalizeValueType(mapping.TargetType)
        };

        if (mapping.Pipeline.Count == 0)
        {
            return CastValue(current, mapping.TargetType, mapping.OnError);
        }

        var transformed = ApplyPipeline(current, mapping.Pipeline, mapping.TargetType, mapping.OnError);
        return transformed with { Field = mapping.TargetFieldKey };
    }

    private static string NormalizeValueType(string type)
    {
        return type switch
        {
            "Int" or "Long" or "Decimal" or "String" or "Text" or "Bool" or "DateTime" or "Date" => type,
            _ => "String"
        };
    }

    private static DynamicFieldValueDto ApplyPipeline(
        DynamicFieldValueDto value,
        IReadOnlyList<DynamicViewTransformOpDto> pipeline,
        string targetType,
        string? onError)
    {
        var current = value;
        foreach (var op in pipeline)
        {
            current = ApplySingleOp(current, op);
        }

        return CastValue(current, targetType, onError);
    }

    private static DynamicFieldValueDto ApplySingleOp(DynamicFieldValueDto value, DynamicViewTransformOpDto op)
    {
        var type = op.Type?.Trim().ToLowerInvariant() ?? string.Empty;
        return type switch
        {
            "trim" => value with { ValueType = "String", StringValue = (ReadAsString(value) ?? string.Empty).Trim() },
            "upper" => value with { ValueType = "String", StringValue = (ReadAsString(value) ?? string.Empty).ToUpperInvariant() },
            "lower" => value with { ValueType = "String", StringValue = (ReadAsString(value) ?? string.Empty).ToLowerInvariant() },
            "replace" => ApplyReplace(value, op.Args),
            "concat" => ApplyConcat(value, op.Args),
            "expr" => ApplyExpr(value, op.Args),
            "cast" => ApplyCastOp(value, op.Args),
            "lookup" => ApplyLookup(value, op.Args),
            _ => value
        };
    }

    private static DynamicFieldValueDto ApplyReplace(DynamicFieldValueDto value, Dictionary<string, object?>? args)
    {
        var current = ReadAsString(value) ?? string.Empty;
        var from = args is not null && args.TryGetValue("from", out var fromObj) ? Convert.ToString(fromObj, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
        var to = args is not null && args.TryGetValue("to", out var toObj) ? Convert.ToString(toObj, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
        return value with { ValueType = "String", StringValue = current.Replace(from, to, StringComparison.Ordinal) };
    }

    private static DynamicFieldValueDto ApplyConcat(DynamicFieldValueDto value, Dictionary<string, object?>? args)
    {
        var left = ReadAsString(value) ?? string.Empty;
        var suffix = args is not null && args.TryGetValue("value", out var val)
            ? Convert.ToString(val, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;
        return value with { ValueType = "String", StringValue = string.Concat(left, suffix) };
    }

    private static DynamicFieldValueDto ApplyExpr(DynamicFieldValueDto value, Dictionary<string, object?>? args)
    {
        if (args is null || !args.TryGetValue("value", out var exprObj))
        {
            return value;
        }

        var expr = Convert.ToString(exprObj, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(expr))
        {
            return value;
        }

        if ((expr.StartsWith('\"') && expr.EndsWith('\"')) || (expr.StartsWith('\'') && expr.EndsWith('\'')))
        {
            return value with { ValueType = "String", StringValue = expr[1..^1] };
        }

        return value;
    }

    private static DynamicFieldValueDto ApplyCastOp(DynamicFieldValueDto value, Dictionary<string, object?>? args)
    {
        var target = args is not null && args.TryGetValue("targetType", out var targetTypeObj)
            ? Convert.ToString(targetTypeObj, CultureInfo.InvariantCulture) ?? "String"
            : "String";
        return CastValue(value, target, null);
    }

    private static DynamicFieldValueDto ApplyLookup(DynamicFieldValueDto value, Dictionary<string, object?>? args)
    {
        var source = ReadAsString(value) ?? string.Empty;
        if (args is null || !args.TryGetValue("map", out var mapObj) || mapObj is not JsonElement json || json.ValueKind != JsonValueKind.Object)
        {
            return value;
        }

        if (json.TryGetProperty(source, out var matched))
        {
            return value with { ValueType = "String", StringValue = matched.ToString() };
        }

        return value;
    }

    private static DynamicFieldValueDto CastValue(DynamicFieldValueDto value, string targetType, string? onError)
    {
        try
        {
            var normalized = NormalizeValueType(targetType);
            var text = ReadAsString(value);
            return normalized switch
            {
                "Int" => value with
                {
                    ValueType = "Int",
                    IntValue = TryParseInt(text),
                    StringValue = null,
                    LongValue = null,
                    DecimalValue = null,
                    BoolValue = null,
                    DateTimeValue = null,
                    DateValue = null
                },
                "Long" => value with
                {
                    ValueType = "Long",
                    LongValue = TryParseLong(text),
                    StringValue = null,
                    IntValue = null,
                    DecimalValue = null,
                    BoolValue = null,
                    DateTimeValue = null,
                    DateValue = null
                },
                "Decimal" => value with
                {
                    ValueType = "Decimal",
                    DecimalValue = TryParseDecimal(text),
                    StringValue = null,
                    IntValue = null,
                    LongValue = null,
                    BoolValue = null,
                    DateTimeValue = null,
                    DateValue = null
                },
                "Bool" => value with
                {
                    ValueType = "Bool",
                    BoolValue = TryParseBool(text),
                    StringValue = null,
                    IntValue = null,
                    LongValue = null,
                    DecimalValue = null,
                    DateTimeValue = null,
                    DateValue = null
                },
                "DateTime" => value with
                {
                    ValueType = "DateTime",
                    DateTimeValue = TryParseDateTime(text),
                    StringValue = null,
                    IntValue = null,
                    LongValue = null,
                    DecimalValue = null,
                    BoolValue = null,
                    DateValue = null
                },
                "Date" => value with
                {
                    ValueType = "Date",
                    DateValue = TryParseDateTime(text),
                    StringValue = null,
                    IntValue = null,
                    LongValue = null,
                    DecimalValue = null,
                    BoolValue = null,
                    DateTimeValue = null
                },
                "Text" or "String" => value with
                {
                    ValueType = "String",
                    StringValue = text,
                    IntValue = null,
                    LongValue = null,
                    DecimalValue = null,
                    BoolValue = null,
                    DateTimeValue = null,
                    DateValue = null
                },
                _ => value
            };
        }
        catch when (!string.Equals(onError, "reject_row", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(onError, "default", StringComparison.OrdinalIgnoreCase)
                ? new DynamicFieldValueDto
                {
                    Field = value.Field,
                    ValueType = NormalizeValueType(targetType),
                    StringValue = string.Empty
                }
                : new DynamicFieldValueDto
                {
                    Field = value.Field,
                    ValueType = NormalizeValueType(targetType)
                };
        }
    }

    private static int? TryParseInt(string? text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static long? TryParseLong(string? text) => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static decimal? TryParseDecimal(string? text) => decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static bool? TryParseBool(string? text) => bool.TryParse(text, out var value) ? value : null;
    private static DateTimeOffset? TryParseDateTime(string? text) => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;

    private static string? ReadAsString(DynamicFieldValueDto? value)
    {
        if (value is null)
        {
            return null;
        }
        return value.ValueType switch
        {
            "String" or "Text" => value.StringValue,
            "Int" => value.IntValue?.ToString(CultureInfo.InvariantCulture),
            "Long" => value.LongValue?.ToString(CultureInfo.InvariantCulture),
            "Decimal" => value.DecimalValue?.ToString(CultureInfo.InvariantCulture),
            "Bool" => value.BoolValue?.ToString(),
            "DateTime" => value.DateTimeValue?.ToString("O", CultureInfo.InvariantCulture),
            "Date" => value.DateValue?.ToString("O", CultureInfo.InvariantCulture),
            _ => value.StringValue
        };
    }

    private static IReadOnlyList<DynamicColumnDef> BuildColumns(List<DynamicRecordDto> items, IReadOnlyList<DynamicViewFieldPlan> mappings)
    {
        if (mappings.Count > 0)
        {
            return mappings.Select(mapping => new DynamicColumnDef(mapping.TargetFieldKey, string.IsNullOrWhiteSpace(mapping.TargetLabel) ? mapping.TargetFieldKey : mapping.TargetLabel, mapping.TargetType, true, true, false)).ToArray();
        }

        var first = items.FirstOrDefault();
        if (first is null)
        {
            return Array.Empty<DynamicColumnDef>();
        }

        return first.Values.Select(value => new DynamicColumnDef(value.Field, value.Field, value.ValueType, true, true, false)).ToArray();
    }

    private static DynamicRecordListResult ApplyViewPostProcessing(DynamicRecordListResult result, DynamicViewExecutionPlan plan)
    {
        var items = result.Items.ToList();
        if (plan.Filters.Count > 0)
        {
            items = items.Where(item => MatchAllFilters(item, plan.Filters)).ToList();
        }

        if (plan.Sorts.Count > 0)
        {
            items = ApplySorts(items, plan.Sorts);
        }

        if (plan.Limit is > 0)
        {
            items = items.Take(plan.Limit.Value).ToList();
        }

        return new DynamicRecordListResult(items, items.Count, result.PageIndex, result.PageSize, result.Columns);
    }

    private static bool MatchAllFilters(DynamicRecordDto item, IReadOnlyList<JsonFilterRuleDto> filters)
    {
        foreach (var filter in filters)
        {
            if (!MatchFilter(item, filter))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchFilter(DynamicRecordDto item, JsonFilterRuleDto filter)
    {
        var actual = item.Values.FirstOrDefault(x => string.Equals(x.Field, filter.Field, StringComparison.OrdinalIgnoreCase));
        var actualText = actual is null ? null : ReadAsString(actual);
        var expected = ToFilterText(filter.Value);
        var op = (filter.Operator ?? "eq").Trim().ToLowerInvariant();

        return op switch
        {
            "eq" => string.Equals(actualText, expected, StringComparison.OrdinalIgnoreCase),
            "ne" => !string.Equals(actualText, expected, StringComparison.OrdinalIgnoreCase),
            "contains" => actualText?.Contains(expected ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "startswith" => actualText?.StartsWith(expected ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "endswith" => actualText?.EndsWith(expected ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "gt" => CompareAsDecimal(actualText, expected) > 0,
            "ge" => CompareAsDecimal(actualText, expected) >= 0,
            "lt" => CompareAsDecimal(actualText, expected) < 0,
            "le" => CompareAsDecimal(actualText, expected) <= 0,
            _ => true
        };
    }

    private static int CompareAsDecimal(string? left, string? right)
    {
        var l = TryParseDecimal(left) ?? 0m;
        var r = TryParseDecimal(right) ?? 0m;
        return l.CompareTo(r);
    }

    private static string? ToFilterText(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement json when json.ValueKind == JsonValueKind.Null => null,
            JsonElement json when json.ValueKind == JsonValueKind.String => json.GetString(),
            JsonElement json => json.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }

    private static List<DynamicRecordDto> ApplySorts(List<DynamicRecordDto> items, IReadOnlyList<DynamicViewSortDto> sorts)
    {
        IOrderedEnumerable<DynamicRecordDto>? ordered = null;
        foreach (var sort in sorts)
        {
            var desc = string.Equals(sort.Direction, "desc", StringComparison.OrdinalIgnoreCase);
            Func<DynamicRecordDto, string?> keySelector = row => ReadAsString(
                row.Values.FirstOrDefault(value => string.Equals(value.Field, sort.Field, StringComparison.OrdinalIgnoreCase))
                ?? new DynamicFieldValueDto { Field = sort.Field, ValueType = "String" });

            if (ordered is null)
            {
                ordered = desc ? items.OrderByDescending(keySelector) : items.OrderBy(keySelector);
            }
            else
            {
                ordered = desc ? ordered.ThenByDescending(keySelector) : ordered.ThenBy(keySelector);
            }
        }

        return (ordered ?? items.OrderBy(_ => 0)).ToList();
    }
}
