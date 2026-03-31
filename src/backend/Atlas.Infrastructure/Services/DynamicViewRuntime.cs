using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Application.DynamicViews.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using System.Globalization;
using System.Text.Json;

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
        var sourceTable = await _tableRepository.FindByKeyAsync(tenantId, plan.SourceTableKey, appId ?? _appContextAccessor.ResolveAppId(), cancellationToken);
        if (sourceTable is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTableNotFound");
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, sourceTable.Id, cancellationToken);
        var queryRequest = new DynamicRecordQueryRequest(
            request.PageIndex,
            request.PageSize,
            request.Keyword,
            request.SortBy,
            request.SortDesc,
            request.Filters)
        {
            AdvancedQuery = request.AdvancedQuery
        };

        var sourceResult = await _recordRepository.QueryAsync(tenantId, sourceTable, fields, queryRequest, cancellationToken);
        if (plan.Fields.Count == 0)
        {
            return ApplyViewPostProcessing(sourceResult, plan);
        }

        var projectedItems = sourceResult.Items
            .Select(item => new DynamicRecordDto(
                item.Id,
                plan.Fields.Select(mapping => ProjectField(item, mapping)).ToArray()))
            .ToArray();

        var columns = plan.Fields
            .Select(mapping => new DynamicColumnDef(
                mapping.TargetFieldKey,
                string.IsNullOrWhiteSpace(mapping.TargetLabel) ? mapping.TargetFieldKey : mapping.TargetLabel,
                mapping.TargetType,
                true,
                true,
                false))
            .ToArray();

        var projected = new DynamicRecordListResult(projectedItems, sourceResult.Total, sourceResult.PageIndex, sourceResult.PageSize, columns);
        return ApplyViewPostProcessing(projected, plan);
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

        // P0：仅支持常量表达式占位，复杂表达式后续由专用引擎接管
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

    private static string? ReadAsString(DynamicFieldValueDto value)
    {
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
