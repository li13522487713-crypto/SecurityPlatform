using System.Text.Json;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicTables.Enums;
using Atlas.Infrastructure.DynamicTables;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicTableCommandService : IDynamicTableCommandService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDynamicTableRepository _tableRepository;
    private readonly IDynamicFieldRepository _fieldRepository;
    private readonly IDynamicIndexRepository _indexRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ISqlSugarClient _db;
    private readonly TimeProvider _timeProvider;

    public DynamicTableCommandService(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicIndexRepository indexRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        ISqlSugarClient db,
        TimeProvider timeProvider)
    {
        _tableRepository = tableRepository;
        _fieldRepository = fieldRepository;
        _indexRepository = indexRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        DynamicTableCreateRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await _tableRepository.FindByKeyAsync(tenantId, request.TableKey, cancellationToken);
        if (existing is not null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "表标识已存在。");
        }

        var dbType = DynamicEnumMapper.ParseDbType(request.DbType);
        if (dbType != DynamicDbType.Sqlite)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "当前仅支持 SQLite 动态建表。");
        }
        var now = _timeProvider.GetUtcNow();
        var table = new DynamicTable(
            tenantId,
            request.TableKey,
            request.DisplayName,
            request.Description,
            dbType,
            userId,
            _idGeneratorAccessor.NextId(),
            now);

        var fields = BuildFields(tenantId, table.Id, request.Fields, now);
        var indexes = BuildIndexes(tenantId, table.Id, request.Indexes, now);

        var createTableSql = DynamicSqlBuilder.BuildCreateTableSql(table, fields);
        var indexSql = BuildCreateIndexSql(table, indexes);
        var ddl = string.IsNullOrWhiteSpace(indexSql) ? createTableSql : $"{createTableSql}\n{indexSql}";

        var result = await _db.Ado.UseTranAsync(async () =>
        {
            await _db.Ado.ExecuteCommandAsync(ddl);
            await _tableRepository.AddAsync(table, cancellationToken);
            await _fieldRepository.AddRangeAsync(fields, cancellationToken);
            await _indexRepository.AddRangeAsync(indexes, cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException(ErrorCodes.ServerError, "创建动态表失败。");
        }

        return table.Id;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicTableUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "动态表不存在。");
        }

        var status = DynamicEnumMapper.ParseStatus(request.Status);
        table.UpdateMeta(request.DisplayName, request.Description, status, userId, _timeProvider.GetUtcNow());
        await _tableRepository.UpdateAsync(table, cancellationToken);
    }

    public Task AlterAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicTableAlterRequest request,
        CancellationToken cancellationToken)
    {
        throw new BusinessException(ErrorCodes.ValidationError, "当前版本暂不支持表结构变更。");
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, cancellationToken);
        if (table is null)
        {
            return;
        }

        var ddl = DynamicSqlBuilder.BuildDropTableSql(table);
        var result = await _db.Ado.UseTranAsync(async () =>
        {
            await _db.Ado.ExecuteCommandAsync(ddl);
            await _fieldRepository.DeleteByTableIdAsync(tenantId, table.Id, cancellationToken);
            await _indexRepository.DeleteByTableIdAsync(tenantId, table.Id, cancellationToken);
            await _tableRepository.DeleteAsync(tenantId, table.Id, cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException(ErrorCodes.ServerError, "删除动态表失败。");
        }
    }

    private IReadOnlyList<DynamicField> BuildFields(
        TenantId tenantId,
        long tableId,
        IReadOnlyList<DynamicFieldDefinition> fields,
        DateTimeOffset now)
    {
        var list = new List<DynamicField>(fields.Count);
        var order = 0;
        foreach (var field in fields)
        {
            var fieldType = DynamicEnumMapper.ParseFieldType(field.FieldType);
            var id = _idGeneratorAccessor.NextId();
            var entity = new DynamicField(
                tenantId,
                tableId,
                field.Name,
                field.DisplayName ?? field.Name,
                fieldType,
                field.Length,
                field.Precision,
                field.Scale,
                field.AllowNull,
                field.IsPrimaryKey,
                field.IsAutoIncrement,
                field.IsUnique,
                field.DefaultValue,
                field.SortOrder > 0 ? field.SortOrder : order++,
                id,
                now);
            list.Add(entity);
        }

        return list;
    }

    private IReadOnlyList<DynamicIndex> BuildIndexes(
        TenantId tenantId,
        long tableId,
        IReadOnlyList<DynamicIndexDefinition> indexes,
        DateTimeOffset now)
    {
        if (indexes.Count == 0)
        {
            return Array.Empty<DynamicIndex>();
        }

        var list = new List<DynamicIndex>(indexes.Count);
        foreach (var index in indexes)
        {
            var fieldsJson = JsonSerializer.Serialize(index.Fields, JsonOptions);
            var entity = new DynamicIndex(
                tenantId,
                tableId,
                index.Name,
                index.IsUnique,
                fieldsJson,
                _idGeneratorAccessor.NextId(),
                now);
            list.Add(entity);
        }

        return list;
    }

    private static string BuildCreateIndexSql(DynamicTable table, IReadOnlyList<DynamicIndex> indexes)
    {
        if (indexes.Count == 0)
        {
            return string.Empty;
        }

        var sqlList = new List<string>(indexes.Count);
        foreach (var index in indexes)
        {
            var fields = JsonSerializer.Deserialize<string[]>(index.FieldsJson, JsonOptions) ?? Array.Empty<string>();
            if (fields.Length == 0)
            {
                continue;
            }

            sqlList.Add(DynamicSqlBuilder.BuildCreateIndexSql(table, fields, index.Name, index.IsUnique));
        }

        return string.Join("\n", sqlList);
    }
}
