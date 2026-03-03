using System.Data;
using System.Text.Json;
using System.Diagnostics;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicTables.Enums;
using Atlas.Infrastructure.DynamicTables;
using Atlas.Infrastructure.Observability;
using SqlSugar;

namespace Atlas.Infrastructure.Repositories;

public sealed class DynamicRecordRepository : IDynamicRecordRepository
{
    private static readonly HashSet<string> SupportedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq", "ne", "gt", "gte", "lt", "lte", "like", "in", "between"
    };

    private readonly ISqlSugarClient _db;

    public DynamicRecordRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<long> InsertAsync(
        Atlas.Core.Tenancy.TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var pk = GetPrimaryKey(fields);
        var values = MapValues(request.Values);

        var columns = new List<string> { DynamicSqlBuilder.QuoteTenantColumn(table.DbType) };
        var parameters = new List<SugarParameter>
        {
            new("@tenantId", tenantId.Value.ToString("D"))
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
                    throw new BusinessException(Atlas.Core.Models.ErrorCodes.ValidationError, $"字段 {field.Name} 必填。");
                }
                continue;
            }

            var value = ResolveValue(field, valueDto);
            var paramName = $"@p{parameters.Count}";
            columns.Add(DynamicSqlBuilder.Quote(field.Name, table.DbType));
            parameters.Add(new SugarParameter(paramName, value ?? DBNull.Value));
        }

        if (columns.Count == 0)
        {
            throw new BusinessException(Atlas.Core.Models.ErrorCodes.ValidationError, "没有可写入的字段。");
        }

        var sql = $"INSERT INTO {DynamicSqlBuilder.Quote(table.TableKey, table.DbType)} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", parameters.Select(p => p.ParameterName))});";
        await _db.Ado.ExecuteCommandAsync(sql, parameters.ToArray());

        if (pk.IsAutoIncrement)
        {
            var id = await ExecuteScalarLongAsync("SELECT last_insert_rowid() AS Id;", Array.Empty<SugarParameter>());
            return id;
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
        var pk = GetPrimaryKey(fields);
        var values = MapValues(request.Values);

        var setClauses = new List<string>();
        var parameters = new List<SugarParameter>
        {
            new("@id", id),
            new("@tenantId", tenantId.Value.ToString("D"))
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
            var paramName = $"@p{parameters.Count}";
            setClauses.Add($"{DynamicSqlBuilder.Quote(field.Name, table.DbType)} = {paramName}");
            parameters.Add(new SugarParameter(paramName, value ?? DBNull.Value));
        }

        if (setClauses.Count == 0)
        {
            return;
        }

        var sql =
            $"UPDATE {DynamicSqlBuilder.Quote(table.TableKey, table.DbType)} " +
            $"SET {string.Join(", ", setClauses)} " +
            $"WHERE {DynamicSqlBuilder.Quote(pk.Name, table.DbType)} = @id " +
            $"AND {DynamicSqlBuilder.QuoteTenantColumn(table.DbType)} = @tenantId;";
        await _db.Ado.ExecuteCommandAsync(sql, parameters.ToArray());
    }

    public Task DeleteAsync(
        Atlas.Core.Tenancy.TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        long id,
        CancellationToken cancellationToken)
    {
        var pk = GetPrimaryKey(fields);
        var sql =
            $"DELETE FROM {DynamicSqlBuilder.Quote(table.TableKey, table.DbType)} " +
            $"WHERE {DynamicSqlBuilder.Quote(pk.Name, table.DbType)} = @id " +
            $"AND {DynamicSqlBuilder.QuoteTenantColumn(table.DbType)} = @tenantId;";
        return _db.Ado.ExecuteCommandAsync(sql, new[]
        {
            new SugarParameter("@id", id),
            new SugarParameter("@tenantId", tenantId.Value.ToString("D"))
        });
    }

    public Task DeleteBatchAsync(
        Atlas.Core.Tenancy.TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Task.CompletedTask;
        }

        var pk = GetPrimaryKey(fields);
        var parameters = ids.Select((id, index) => new SugarParameter($"@p{index}", id)).ToList();
        var inClause = string.Join(", ", parameters.Select(p => p.ParameterName));
        parameters.Add(new SugarParameter("@tenantId", tenantId.Value.ToString("D")));
        var sql =
            $"DELETE FROM {DynamicSqlBuilder.Quote(table.TableKey, table.DbType)} " +
            $"WHERE {DynamicSqlBuilder.Quote(pk.Name, table.DbType)} IN ({inClause}) " +
            $"AND {DynamicSqlBuilder.QuoteTenantColumn(table.DbType)} = @tenantId;";
        return _db.Ado.ExecuteCommandAsync(sql, parameters.ToArray());
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
                throw new BusinessException(Atlas.Core.Models.ErrorCodes.ValidationError, "当前仅支持 SQLite 动态数据查询。");
            }

            var where = BuildWhereClause(tenantId, table, fields, request, out var parameters);
            var totalSql = $"SELECT COUNT(1) AS Total FROM {DynamicSqlBuilder.Quote(table.TableKey, table.DbType)} {where};";
            var total = (int)await ExecuteScalarLongAsync(totalSql, parameters.ToArray());

            var orderBy = BuildOrderBy(table, fields, request);
            var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
            var pageSize = request.PageSize < 1 ? 20 : request.PageSize;
            var offset = (pageIndex - 1) * pageSize;

            var pagingParameters = new List<SugarParameter>(parameters)
            {
                new SugarParameter("@limit", pageSize),
                new SugarParameter("@offset", offset)
            };

            var selectColumns = string.Join(", ", fields.Select(f => DynamicSqlBuilder.Quote(f.Name, table.DbType)));
            var sql = $"SELECT {selectColumns} FROM {DynamicSqlBuilder.Quote(table.TableKey, table.DbType)} {where} {orderBy} LIMIT @limit OFFSET @offset;";
            var dataTable = await _db.Ado.GetDataTableAsync(sql, pagingParameters.ToArray());
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
        var sql =
            $"SELECT {string.Join(", ", fields.Select(f => DynamicSqlBuilder.Quote(f.Name, table.DbType)))} " +
            $"FROM {DynamicSqlBuilder.Quote(table.TableKey, table.DbType)} " +
            $"WHERE {DynamicSqlBuilder.Quote(pk.Name, table.DbType)} = @id " +
            $"AND {DynamicSqlBuilder.QuoteTenantColumn(table.DbType)} = @tenantId;";
        var dataTable = await _db.Ado.GetDataTableAsync(sql, new[]
        {
            new SugarParameter("@id", id),
            new SugarParameter("@tenantId", tenantId.Value.ToString("D"))
        });
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
            throw new BusinessException(Atlas.Core.Models.ErrorCodes.ValidationError, "未配置主键字段。");
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
        DynamicTable table,
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
            $"{DynamicSqlBuilder.QuoteTenantColumn(table.DbType)} = @tenantId"
        };

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = $"%{request.Keyword.Trim()}%";
            var keywordConditions = new List<string>();
            foreach (var field in fields.Where(x => x.FieldType is DynamicFieldType.String or DynamicFieldType.Text))
            {
                var paramName = $"@p{parameters.Count}";
                keywordConditions.Add($"{DynamicSqlBuilder.Quote(field.Name, table.DbType)} LIKE {paramName}");
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
            if (string.IsNullOrWhiteSpace(filter.Field) || !SupportedOperators.Contains(filter.Operator))
            {
                continue;
            }

            var field = fields.FirstOrDefault(x => string.Equals(x.Name, filter.Field, StringComparison.OrdinalIgnoreCase));
            if (field is null)
            {
                continue;
            }

            var fieldName = DynamicSqlBuilder.Quote(field.Name, table.DbType);
            var op = filter.Operator.ToLowerInvariant();

            if (op == "in" && filter.Value is { ValueKind: JsonValueKind.Array })
            {
                var values = filter.Value.Value.EnumerateArray().Select(x => x.ToString()).ToArray();
                if (values.Length == 0)
                {
                    continue;
                }

                var paramNames = new List<string>();
                foreach (var item in values)
                {
                    var paramName = $"@p{parameters.Count}";
                    paramNames.Add(paramName);
                    parameters.Add(new SugarParameter(paramName, item));
                }

                conditions.Add($"{fieldName} IN ({string.Join(", ", paramNames)})");
                continue;
            }

            if (op == "between" && filter.Value is { ValueKind: JsonValueKind.Array })
            {
                var values = filter.Value.Value.EnumerateArray().Select(x => x.ToString()).ToArray();
                if (values.Length < 2)
                {
                    continue;
                }

                var startParam = $"@p{parameters.Count}";
                parameters.Add(new SugarParameter(startParam, values[0]));
                var endParam = $"@p{parameters.Count}";
                parameters.Add(new SugarParameter(endParam, values[1]));
                conditions.Add($"{fieldName} BETWEEN {startParam} AND {endParam}");
                continue;
            }

            var param = $"@p{parameters.Count}";
            parameters.Add(new SugarParameter(param, filter.Value?.ToString()));
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
            conditions.Add($"{fieldName} {sqlOp} {param}");
        }

        return $"WHERE {string.Join(" AND ", conditions)}";
    }

    private static string BuildOrderBy(
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        DynamicRecordQueryRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.SortBy))
        {
            var field = fields.FirstOrDefault(x => string.Equals(x.Name, request.SortBy, StringComparison.OrdinalIgnoreCase));
            if (field is not null)
            {
                var dir = request.SortDesc ? "DESC" : "ASC";
                return $"ORDER BY {DynamicSqlBuilder.Quote(field.Name, table.DbType)} {dir}";
            }
        }

        var pk = GetPrimaryKey(fields);
        return $"ORDER BY {DynamicSqlBuilder.Quote(pk.Name, table.DbType)} DESC";
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
            field.FieldType == DynamicFieldType.Bool ? "status" : "text",
            true,
            field.FieldType is DynamicFieldType.String or DynamicFieldType.Text,
            false)).ToArray();
    }

    private async Task<long> ExecuteScalarLongAsync(string sql, SugarParameter[] parameters)
    {
        var table = await _db.Ado.GetDataTableAsync(sql, parameters);
        if (table.Rows.Count == 0)
        {
            return 0;
        }

        return Convert.ToInt64(table.Rows[0][0]);
    }
}
