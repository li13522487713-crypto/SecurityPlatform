using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Globalization;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicTables.Enums;
using Atlas.Infrastructure.DynamicTables;
using Atlas.Infrastructure.Observability;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicRecordRepository : IDynamicRecordRepository
{
    private static readonly HashSet<string> SupportedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq", "ne", "gt", "gte", "lt", "lte", "like", "in", "between"
    };

    private readonly ISqlSugarClient _mainDb;
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public DynamicRecordRepository(
        ISqlSugarClient mainDb,
        IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = mainDb;
        _appDbScopeFactory = appDbScopeFactory;
    }

    public DynamicRecordRepository(ISqlSugarClient db)
        : this(db, new MainOnlyAppDbScopeFactory(db))
    {
    }

    public async Task<long> InsertAsync(
        Atlas.Core.Tenancy.TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, table.AppId, cancellationToken);
        var pk = GetPrimaryKey(fields);
        var values = MapValues(request.Values);

        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [DynamicSqlBuilder.TenantColumnName] = tenantId.Value.ToString("D")
        };

        foreach (var field in fields)
        {
            if (field.IsPrimaryKey && field.IsAutoIncrement)
            {
                continue;
            }

            if (!values.TryGetValue(field.Name, out var valueDto))
            {
                if (!field.AllowNull)
                {
                    throw new BusinessException(Atlas.Core.Models.ErrorCodes.ValidationError, $"Field '{field.Name}' is required.");
                }
                continue;
            }

            var value = ResolveValue(field, valueDto);
            row[field.Name] = value ?? DBNull.Value;
        }

        if (row.Count <= 1)
        {
            throw new BusinessException(Atlas.Core.Models.ErrorCodes.ValidationError, "No writable fields.");
        }

        await db.Insertable(row).AS(table.TableKey).ExecuteCommandAsync();

        if (pk.IsAutoIncrement)
        {
            var latest = await db.Queryable<object>()
                .AS(table.TableKey)
                .OrderBy($"{pk.Name} desc")
                .Select<long>(pk.Name)
                .FirstAsync();
            return latest;
        }

        if (values.TryGetValue(pk.Name, out var pkValue))
        {
            var resolved = ResolveValue(pk, pkValue);
            return Convert.ToInt64(resolved);
        }

        return 0;
    }

    public async Task UpdateAsync(
        Atlas.Core.Tenancy.TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        long id,
        DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var db = await GetDbAsync(tenantId, table.AppId, cancellationToken);
        var pk = GetPrimaryKey(fields);
        var values = MapValues(request.Values);

        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [pk.Name] = id,
            [DynamicSqlBuilder.TenantColumnName] = tenantId.Value.ToString("D")
        };

        foreach (var field in fields)
        {
            if (field.IsPrimaryKey)
            {
                continue;
            }

            if (!values.TryGetValue(field.Name, out var valueDto))
            {
                continue;
            }

            var value = ResolveValue(field, valueDto);
            row[field.Name] = value ?? DBNull.Value;
        }

        if (row.Count <= 2)
        {
            return;
        }

        await db.Updateable(row)
            .AS(table.TableKey)
            .WhereColumns(pk.Name, DynamicSqlBuilder.TenantColumnName)
            .ExecuteCommandAsync();
    }

    public async Task DeleteAsync(
        Atlas.Core.Tenancy.TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        long id,
        CancellationToken cancellationToken)
    {
        var pk = GetPrimaryKey(fields);
        var db = await GetDbAsync(tenantId, table.AppId, cancellationToken);
        await db.Deleteable<object>()
            .AS(table.TableKey)
            .Where($"{pk.Name} = @id and {DynamicSqlBuilder.TenantColumnName} = @tenantId", new
            {
                id,
                tenantId = tenantId.Value.ToString("D")
            })
            .ExecuteCommandAsync();
    }

    public async Task DeleteBatchAsync(
        Atlas.Core.Tenancy.TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var pk = GetPrimaryKey(fields);
        var idList = string.Join(", ", ids);
        var db = await GetDbAsync(tenantId, table.AppId, cancellationToken);
        await db.Deleteable<object>()
            .AS(table.TableKey)
            .Where($"{pk.Name} in ({idList}) and {DynamicSqlBuilder.TenantColumnName} = @tenantId", new
            {
                tenantId = tenantId.Value.ToString("D")
            })
            .ExecuteCommandAsync();
    }

    public async Task<DynamicRecordListResult> QueryAsync(
        Atlas.Core.Tenancy.TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        DynamicRecordQueryRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var status = "success";
        try
        {
            if (table.DbType != DynamicDbType.Sqlite)
            {
                throw new BusinessException(Atlas.Core.Models.ErrorCodes.ValidationError, "Only SQLite is supported for dynamic data queries.");
            }

            var where = BuildWhereClause(tenantId, fields, request, out var parameters);
            var db = await GetDbAsync(tenantId, table.AppId, cancellationToken);
            var query = db.Queryable<object>()
                .AS(table.TableKey)
                .Where(where, parameters.ToArray());
            var total = await query.CountAsync();

            var orderBy = BuildOrderBy(fields, request);
            var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
            var pageSize = request.PageSize < 1 ? 20 : request.PageSize;
            var dataTable = await query
                .OrderBy(orderBy)
                .ToDataTablePageAsync(pageIndex, pageSize);
            var items = BuildRecords(fields, dataTable);

            return new DynamicRecordListResult(
                items,
                total,
                pageIndex,
                pageSize,
                BuildColumns(fields));
        }
        catch
        {
            status = "failed";
            throw;
        }
        finally
        {
            AtlasMetrics.RecordDynamicQuery(stopwatch.Elapsed.TotalMilliseconds, status);
        }
    }

    public async Task<DynamicRecordDto?> GetByIdAsync(
        Atlas.Core.Tenancy.TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        long id,
        CancellationToken cancellationToken)
    {
        var pk = GetPrimaryKey(fields);
        var db = await GetDbAsync(tenantId, table.AppId, cancellationToken);
        var dataTable = await db.Queryable<object>()
            .AS(table.TableKey)
            .Where($"{pk.Name} = @id and {DynamicSqlBuilder.TenantColumnName} = @tenantId", new
            {
                id,
                tenantId = tenantId.Value.ToString("D")
            })
            .ToDataTableAsync();
        if (dataTable.Rows.Count == 0)
        {
            return null;
        }

        return BuildRecord(fields, dataTable.Rows[0]);
    }

    private static DynamicField GetPrimaryKey(IReadOnlyList<DynamicField> fields)
    {
        var pk = fields.FirstOrDefault(x => x.IsPrimaryKey);
        if (pk is null)
        {
            throw new BusinessException(Atlas.Core.Models.ErrorCodes.ValidationError, "Primary key field is not configured.");
        }

        return pk;
    }


    private static Dictionary<string, DynamicFieldValueDto> MapValues(IReadOnlyList<DynamicFieldValueDto> values)
    {
        return values.ToDictionary(v => v.Field, StringComparer.OrdinalIgnoreCase);
    }

    private static object? ResolveValue(DynamicField field, DynamicFieldValueDto value)
    {
        return field.FieldType switch
        {
            DynamicFieldType.Int => value.IntValue,
            DynamicFieldType.Long => value.LongValue,
            DynamicFieldType.Decimal => value.DecimalValue,
            DynamicFieldType.Bool => value.BoolValue,
            DynamicFieldType.DateTime => value.DateTimeValue?.UtcDateTime,
            DynamicFieldType.Date => value.DateValue?.UtcDateTime.Date,
            _ => value.StringValue
        };
    }

    private static string BuildWhereClause(
        Atlas.Core.Tenancy.TenantId tenantId,
        IReadOnlyList<DynamicField> fields,
        DynamicRecordQueryRequest request,
        out List<SugarParameter> parameters)
    {
        parameters = new List<SugarParameter>
        {
            new("@tenantId", tenantId.Value.ToString("D"))
        };
        var conditions = new List<string>
        {
            $"{DynamicSqlBuilder.TenantColumnName} = @tenantId"
        };

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = $"%{request.Keyword.Trim()}%";
            var keywordConditions = new List<string>();
            foreach (var field in fields.Where(x => x.FieldType is DynamicFieldType.String or DynamicFieldType.Text))
            {
                var paramName = $"@p{parameters.Count}";
                keywordConditions.Add($"{field.Name} LIKE {paramName}");
                parameters.Add(new SugarParameter(paramName, keyword));
            }

            if (keywordConditions.Count > 0)
            {
                conditions.Add($"({string.Join(" OR ", keywordConditions)})");
            }
        }

        var filters = request.Filters ?? Array.Empty<DynamicFilterCondition>();
        foreach (var filter in filters)
        {
            var ruleCond = BuildQueryRuleCondition(filter.Field, filter.Operator, filter.Value, fields, parameters);
            if (!string.IsNullOrWhiteSpace(ruleCond))
            {
                conditions.Add(ruleCond);
            }
        }

        if (request.AdvancedQuery?.RootGroup is not null)
        {
            var advancedCondition = BuildQueryGroup(request.AdvancedQuery.RootGroup, fields, parameters);
            if (!string.IsNullOrWhiteSpace(advancedCondition))
            {
                conditions.Add($"({advancedCondition})");
            }
        }

        return string.Join(" AND ", conditions);
    }

    private static string? BuildQueryGroup(
        QueryGroup group,
        IReadOnlyList<DynamicField> fields,
        List<SugarParameter> parameters)
    {
        var groupConditions = new List<string>();

        if (group.Rules != null)
        {
            foreach (var rule in group.Rules)
            {
                var cond = BuildQueryRuleCondition(rule.Field, rule.Operator, rule.Value, fields, parameters);
                if (!string.IsNullOrWhiteSpace(cond))
                {
                    groupConditions.Add(cond);
                }
            }
        }

        if (group.Groups != null)
        {
            foreach (var subGroup in group.Groups)
            {
                var cond = BuildQueryGroup(subGroup, fields, parameters);
                if (!string.IsNullOrWhiteSpace(cond))
                {
                    groupConditions.Add($"({cond})");
                }
            }
        }

        if (groupConditions.Count == 0) return null;

        var conj = string.Equals(group.Conjunction, "or", StringComparison.OrdinalIgnoreCase) ? " OR " : " AND ";
        return string.Join(conj, groupConditions);
    }

    private static string? BuildQueryRuleCondition(
        string filterField,
        string filterOperator,
        JsonElement? filterValue,
        IReadOnlyList<DynamicField> fields,
        List<SugarParameter> parameters)
    {
        if (string.IsNullOrWhiteSpace(filterField) || !SupportedOperators.Contains(filterOperator))
        {
            return null;
        }

        var field = fields.FirstOrDefault(x => string.Equals(x.Name, filterField, StringComparison.OrdinalIgnoreCase));
        if (field is null)
        {
            return null;
        }

        var fieldName = field.Name;
        var op = filterOperator.ToLowerInvariant();

        if (op == "in" && filterValue is { ValueKind: JsonValueKind.Array })
        {
            var values = filterValue.Value
                .EnumerateArray()
                .Select(x => ConvertFilterValue(field, x))
                .Where(x => x is not null)
                .ToArray();
            if (values.Length == 0)
            {
                return null;
            }

            var paramNames = new List<string>();
            foreach (var item in values)
            {
                var paramName = $"@p{parameters.Count}";
                paramNames.Add(paramName);
                parameters.Add(new SugarParameter(paramName, item!));
            }

            return $"{fieldName} IN ({string.Join(", ", paramNames)})";
        }

        if (op == "between" && filterValue is { ValueKind: JsonValueKind.Array })
        {
            var values = filterValue.Value
                .EnumerateArray()
                .Select(x => ConvertFilterValue(field, x))
                .Where(x => x is not null)
                .ToArray();
            if (values.Length < 2)
            {
                return null;
            }

            var startParam = $"@p{parameters.Count}";
            parameters.Add(new SugarParameter(startParam, values[0]!));
            var endParam = $"@p{parameters.Count}";
            parameters.Add(new SugarParameter(endParam, values[1]!));
            return $"{fieldName} BETWEEN {startParam} AND {endParam}";
        }

        if (filterValue is not { } scalarValue)
        {
            return null;
        }

        var resolvedValue = ConvertFilterValue(field, scalarValue);
        if (resolvedValue is null)
        {
            return null;
        }

        if (op == "like" && resolvedValue is string likeValue && !likeValue.Contains('%'))
        {
            resolvedValue = $"%{likeValue}%";
        }

        var param = $"@p{parameters.Count}";
        parameters.Add(new SugarParameter(param, resolvedValue));
        var sqlOp = op switch
        {
            "eq" => "=",
            "ne" => "!=",
            "gt" => ">",
            "gte" => ">=",
            "lt" => "<",
            "lte" => "<=",
            "like" => "LIKE",
            _ => "="
        };
        return $"{fieldName} {sqlOp} {param}";
    }

    private static object? ConvertFilterValue(DynamicField field, JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        try
        {
            return field.FieldType switch
            {
                DynamicFieldType.Int => value.ValueKind == JsonValueKind.Number
                    ? value.GetInt32()
                    : int.Parse(value.ToString(), CultureInfo.InvariantCulture),
                DynamicFieldType.Long => value.ValueKind == JsonValueKind.Number
                    ? value.GetInt64()
                    : long.Parse(value.ToString(), CultureInfo.InvariantCulture),
                DynamicFieldType.Decimal => value.ValueKind == JsonValueKind.Number
                    ? value.GetDecimal()
                    : decimal.Parse(value.ToString(), CultureInfo.InvariantCulture),
                DynamicFieldType.Bool => value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number => value.GetInt32() == 1,
                    _ => bool.Parse(value.ToString())
                },
                DynamicFieldType.DateTime => value.ValueKind == JsonValueKind.String
                    ? DateTimeOffset.Parse(value.GetString()!)
                    : DateTimeOffset.Parse(value.ToString()),
                DynamicFieldType.Date => value.ValueKind == JsonValueKind.String
                    ? DateTimeOffset.Parse(value.GetString()!).Date
                    : DateTimeOffset.Parse(value.ToString()).Date,
                _ => value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : value.ToString()
            };
        }
        catch
        {
            return null;
        }
    }

    private static string BuildOrderBy(
        IReadOnlyList<DynamicField> fields,
        DynamicRecordQueryRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SortBy))
        {
            var field = fields.FirstOrDefault(x => string.Equals(x.Name, request.SortBy, StringComparison.OrdinalIgnoreCase));
            if (field is not null)
            {
                var dir = request.SortDesc ? "DESC" : "ASC";
                return $"{field.Name} {dir}";
            }
        }

        var pk = GetPrimaryKey(fields);
        return $"{pk.Name} DESC";
    }

    private static IReadOnlyList<DynamicRecordDto> BuildRecords(IReadOnlyList<DynamicField> fields, DataTable table)
    {
        var list = new List<DynamicRecordDto>(table.Rows.Count);
        foreach (DataRow row in table.Rows)
        {
            list.Add(BuildRecord(fields, row));
        }

        return list;
    }

    private static DynamicRecordDto BuildRecord(IReadOnlyList<DynamicField> fields, DataRow row)
    {
        var values = new List<DynamicFieldValueDto>(fields.Count);
        var pk = fields.FirstOrDefault(x => x.IsPrimaryKey);
        foreach (var field in fields)
        {
            if (!row.Table.Columns.Contains(field.Name))
            {
                continue;
            }

            var raw = row[field.Name];
            if (raw == DBNull.Value)
            {
                continue;
            }

            values.Add(BuildFieldValue(field, raw));
        }

        long id = 0;
        if (pk is not null && row.Table.Columns.Contains(pk.Name))
        {
            var rawId = row[pk.Name];
            if (rawId != DBNull.Value)
            {
                id = Convert.ToInt64(rawId);
            }
        }

        return new DynamicRecordDto(id.ToString(), values);
    }

    private static DynamicFieldValueDto BuildFieldValue(DynamicField field, object raw)
    {
        return field.FieldType switch
        {
            DynamicFieldType.Int => new DynamicFieldValueDto
            {
                Field = field.Name,
                ValueType = "Int",
                IntValue = Convert.ToInt32(raw)
            },
            DynamicFieldType.Long => new DynamicFieldValueDto
            {
                Field = field.Name,
                ValueType = "Long",
                LongValue = Convert.ToInt64(raw)
            },
            DynamicFieldType.Decimal => new DynamicFieldValueDto
            {
                Field = field.Name,
                ValueType = "Decimal",
                DecimalValue = Convert.ToDecimal(raw)
            },
            DynamicFieldType.Bool => new DynamicFieldValueDto
            {
                Field = field.Name,
                ValueType = "Bool",
                BoolValue = Convert.ToInt32(raw) == 1
            },
            DynamicFieldType.DateTime => new DynamicFieldValueDto
            {
                Field = field.Name,
                ValueType = "DateTime",
                DateTimeValue = TryParseDate(raw)
            },
            DynamicFieldType.Date => new DynamicFieldValueDto
            {
                Field = field.Name,
                ValueType = "Date",
                DateValue = TryParseDate(raw)
            },
            _ => new DynamicFieldValueDto
            {
                Field = field.Name,
                ValueType = "String",
                StringValue = raw.ToString()
            }
        };
    }

    private static DateTimeOffset? TryParseDate(object raw)
    {
        if (raw is DateTimeOffset offset)
        {
            return offset;
        }

        if (raw is DateTime dt)
        {
            return new DateTimeOffset(dt);
        }

        if (DateTimeOffset.TryParse(raw.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static IReadOnlyList<DynamicColumnDef> BuildColumns(IReadOnlyList<DynamicField> fields)
    {
        return fields.Select(field => new DynamicColumnDef(
            field.Name,
            string.IsNullOrWhiteSpace(field.DisplayName) ? field.Name : field.DisplayName,
            field.FieldType.ToString(),
            true,
            field.FieldType is DynamicFieldType.String or DynamicFieldType.Text,
            false)).ToArray();
    }

    private async Task<ISqlSugarClient> GetDbAsync(
        Atlas.Core.Tenancy.TenantId tenantId,
        long? appId,
        CancellationToken cancellationToken)
    {
        if (appId.HasValue && appId.Value > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
        }

        return _mainDb;
    }

}
