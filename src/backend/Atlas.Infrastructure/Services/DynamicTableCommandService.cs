using System.Text.Json;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
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
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicTableCommandService : IDynamicTableCommandService
{
    private static readonly Regex FieldNamePattern = new("^[A-Za-z][A-Za-z0-9_]{1,63}$", RegexOptions.Compiled);
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "select", "from", "where", "table", "index", "create", "drop", "delete", "update", "insert", "alter",
        DynamicSqlBuilder.TenantColumnName
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDynamicTableRepository _tableRepository;
    private readonly IDynamicFieldRepository _fieldRepository;
    private readonly IDynamicIndexRepository _indexRepository;
    private readonly IDynamicRelationRepository _relationRepository;
    private readonly IFieldPermissionRepository _fieldPermissionRepository;
    private readonly IDynamicRecordRepository _recordRepository;
    private readonly IDynamicSchemaMigrationRepository? _migrationRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ISqlSugarClient _db;
    private readonly TimeProvider _timeProvider;
    private readonly IApprovalRuntimeCommandService? _approvalRuntimeService;

    public DynamicTableCommandService(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicIndexRepository indexRepository,
        IDynamicRelationRepository relationRepository,
        IFieldPermissionRepository fieldPermissionRepository,
        IDynamicRecordRepository recordRepository,
        IDynamicSchemaMigrationRepository? migrationRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        ISqlSugarClient db,
        TimeProvider timeProvider,
        IApprovalRuntimeCommandService? approvalRuntimeService = null)
    {
        _tableRepository = tableRepository;
        _fieldRepository = fieldRepository;
        _indexRepository = indexRepository;
        _relationRepository = relationRepository;
        _fieldPermissionRepository = fieldPermissionRepository;
        _recordRepository = recordRepository;
        _migrationRepository = migrationRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _db = db;
        _timeProvider = timeProvider;
        _approvalRuntimeService = approvalRuntimeService;
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

    public async Task AlterAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicTableAlterRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "动态表不存在。");
        }

        if (table.DbType != DynamicDbType.Sqlite)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "当前仅支持 SQLite 动态表结构变更。");
        }

        if (request.RemoveFields.Count > 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "当前版本暂不支持字段删除。");
        }

        if (request.AddFields.Count == 0 && request.UpdateFields.Count == 0)
        {
            return;
        }

        var existingFields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var existingNames = existingFields
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ValidateAddFieldDefinitions(request.AddFields, existingNames);
        var now = _timeProvider.GetUtcNow();
        var newFields = BuildAddFields(tenantId, table.Id, request.AddFields, existingFields, now);
        var fieldsToUpdate = BuildUpdatedFields(request.UpdateFields, existingFields, now);
        var ddlCommands = new List<string>(newFields.Count * 2);
        var generatedIndexes = new List<DynamicIndex>();
        foreach (var field in newFields)
        {
            ddlCommands.Add(DynamicSqlBuilder.BuildAddColumnSql(table, field));

            if (field.IsUnique)
            {
                var indexName = $"uk_{table.TableKey}_{field.Name}".ToLowerInvariant();
                ddlCommands.Add(DynamicSqlBuilder.BuildCreateIndexSql(table, new[] { field.Name }, indexName, true));
                generatedIndexes.Add(new DynamicIndex(
                    tenantId,
                    table.Id,
                    indexName,
                    true,
                    JsonSerializer.Serialize(new[] { field.Name }, JsonOptions),
                    _idGeneratorAccessor.NextId(),
                    now));
            }
        }

        table.UpdateMeta(table.DisplayName, table.Description, table.Status, userId, now);
        var migrationRecord = BuildMigrationRecord(
            tenantId,
            table,
            ResolveOperationType(newFields.Count, fieldsToUpdate.Count),
            BuildMigrationSqlScripts(ddlCommands, fieldsToUpdate),
            userId,
            now);
        var result = await _db.Ado.UseTranAsync(async () =>
        {
            foreach (var ddl in ddlCommands)
            {
                await _db.Ado.ExecuteCommandAsync(ddl);
            }

            await _fieldRepository.AddRangeAsync(newFields, cancellationToken);
            await _fieldRepository.UpdateRangeAsync(fieldsToUpdate, cancellationToken);
            await _indexRepository.AddRangeAsync(generatedIndexes, cancellationToken);
            await _tableRepository.UpdateAsync(table, cancellationToken);
            if (_migrationRepository is not null)
            {
                await _migrationRepository.AddAsync(migrationRecord, cancellationToken);
            }
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException(ErrorCodes.ServerError, "动态表结构变更失败。");
        }
    }

    public async Task<DynamicTableAlterPreviewResponse> PreviewAlterAsync(
        TenantId tenantId,
        string tableKey,
        DynamicTableAlterRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "动态表不存在。");
        }

        if (table.DbType != DynamicDbType.Sqlite)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "当前仅支持 SQLite 动态表结构变更预览。");
        }

        if (request.RemoveFields.Count > 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "当前版本暂不支持字段删除。");
        }

        if (request.AddFields.Count == 0 && request.UpdateFields.Count == 0)
        {
            return new DynamicTableAlterPreviewResponse(tableKey, "NOOP", Array.Empty<string>(), "未检测到可执行变更。");
        }

        var existingFields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var existingNames = existingFields
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        ValidateAddFieldDefinitions(request.AddFields, existingNames);

        var previewFields = BuildPreviewFields(tenantId, table.Id, request.AddFields, existingFields);
        var previewUpdatedFields = BuildPreviewUpdatedFields(request.UpdateFields, existingFields, DateTimeOffset.UtcNow);
        var sqlScripts = BuildMigrationSqlScripts(BuildAddFieldSqlScripts(table, previewFields), previewUpdatedFields);
        return new DynamicTableAlterPreviewResponse(
            tableKey,
            ResolveOperationType(previewFields.Count, previewUpdatedFields.Count),
            sqlScripts,
            "当前版本不支持自动回滚，请通过备份恢复。");
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
            await _relationRepository.DeleteByTableIdAsync(tenantId, table.Id, cancellationToken);
            await _fieldPermissionRepository.ReplaceByTableKeyAsync(tenantId, tableKey, Array.Empty<FieldPermission>(), cancellationToken);
            await _tableRepository.DeleteAsync(tenantId, table.Id, cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException(ErrorCodes.ServerError, "删除动态表失败。");
        }
    }

    public async Task SetRelationsAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRelationUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "动态表不存在。");
        }

        var relations = request.Relations ?? Array.Empty<DynamicRelationDefinition>();
        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var sourceFieldSet = fields.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var relatedTableKeys = relations
            .Select(x => x.RelatedTableKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var relatedTables = await _tableRepository.QueryByKeysAsync(tenantId, relatedTableKeys, cancellationToken);
        var relatedTableMap = relatedTables.ToDictionary(x => x.TableKey, StringComparer.OrdinalIgnoreCase);
        if (relatedTableMap.Count != relatedTableKeys.Length)
        {
            var missing = relatedTableKeys.Where(x => !relatedTableMap.ContainsKey(x)).ToArray();
            throw new BusinessException(ErrorCodes.ValidationError, $"关联表不存在：{string.Join(", ", missing)}");
        }

        var relatedTableIds = relatedTables.Select(x => x.Id).Distinct().ToArray();
        var relatedFields = await _fieldRepository.ListByTableIdsAsync(tenantId, relatedTableIds, cancellationToken);
        var relatedFieldMap = relatedFields
            .GroupBy(x => x.TableId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => y.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));
        var now = _timeProvider.GetUtcNow();
        var relationEntities = new List<DynamicRelation>(relations.Count);
        foreach (var relation in relations)
        {
            if (!sourceFieldSet.Contains(relation.SourceField))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"源字段 {relation.SourceField} 不存在。");
            }

            var relatedTable = relatedTableMap[relation.RelatedTableKey];
            if (!relatedFieldMap.TryGetValue(relatedTable.Id, out var targetSet))
            {
                targetSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            if (!targetSet.Contains(relation.TargetField))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"关联字段 {relation.TargetField} 在表 {relation.RelatedTableKey} 中不存在。");
            }

            relationEntities.Add(new DynamicRelation(
                tenantId,
                table.Id,
                relation.RelatedTableKey,
                relation.SourceField,
                relation.TargetField,
                relation.RelationType,
                relation.CascadeRule,
                _idGeneratorAccessor.NextId(),
                now));
        }

        var tran = await _db.Ado.UseTranAsync(async () =>
        {
            await _relationRepository.DeleteByTableIdAsync(tenantId, table.Id, cancellationToken);
            await _relationRepository.AddRangeAsync(relationEntities, cancellationToken);
        });

        if (!tran.IsSuccess)
        {
            throw tran.ErrorException ?? new BusinessException(ErrorCodes.ServerError, "更新动态表关系失败。");
        }
    }

    public async Task SetFieldPermissionsAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicFieldPermissionUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "动态表不存在。");
        }

        var rules = request.Permissions ?? Array.Empty<DynamicFieldPermissionRule>();
        var fieldNames = (await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken))
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalid = rules
            .Where(x => !fieldNames.Contains(x.FieldName))
            .Select(x => x.FieldName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (invalid.Length > 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, $"字段不存在：{string.Join(", ", invalid)}");
        }

        var now = _timeProvider.GetUtcNow();
        var entities = rules.Select(x => new FieldPermission(
            tenantId,
            tableKey,
            x.FieldName,
            x.RoleCode,
            x.CanView,
            x.CanEdit,
            _idGeneratorAccessor.NextId(),
            now)).ToArray();

        await _fieldPermissionRepository.ReplaceByTableKeyAsync(tenantId, tableKey, entities, cancellationToken);
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

    public async Task BindApprovalFlowAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicTableApprovalBindingRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "动态表不存在。");
        }

        var now = _timeProvider.GetUtcNow();

        if (request.ApprovalFlowDefinitionId.HasValue && !string.IsNullOrWhiteSpace(request.ApprovalStatusField))
        {
            // 验证状态字段是否存在于动态表中
            var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
            var hasField = fields.Any(f => f.Name.Equals(request.ApprovalStatusField, StringComparison.OrdinalIgnoreCase));
            if (!hasField)
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"状态字段 '{request.ApprovalStatusField}' 在动态表 '{tableKey}' 中不存在。");
            }

            table.BindApprovalFlow(request.ApprovalFlowDefinitionId.Value, request.ApprovalStatusField, userId, now);
        }
        else
        {
            table.UnbindApprovalFlow(userId, now);
        }

        await _tableRepository.UpdateAsync(table, cancellationToken);
    }

    public async Task<DynamicTableApprovalSubmitResponse> SubmitApprovalAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        long recordId,
        CancellationToken cancellationToken)
    {
        if (_approvalRuntimeService is null)
        {
            throw new BusinessException(ErrorCodes.ServerError, "审批服务不可用。");
        }

        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "动态表不存在。");
        }

        if (!table.ApprovalFlowDefinitionId.HasValue || string.IsNullOrWhiteSpace(table.ApprovalStatusField))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "该动态表未绑定审批流。");
        }

        // 读取记录数据，构建 DataJson
        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var record = await _recordRepository.GetByIdAsync(tenantId, table, fields, recordId, cancellationToken);
        if (record is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "记录不存在。");
        }

        // 将记录值转为 JSON 对象作为审批实例的 DataJson
        var dataDict = new Dictionary<string, object?>();
        foreach (var val in record.Values)
        {
            object? value = val.ValueType switch
            {
                "Int" => val.IntValue,
                "Long" => val.LongValue,
                "Decimal" => val.DecimalValue,
                "Bool" => val.BoolValue,
                "DateTime" => val.DateTimeValue?.ToString("O"),
                "Date" => val.DateValue?.ToString("O"),
                _ => val.StringValue
            };
            dataDict[val.Field] = value;
        }
        dataDict["_tableKey"] = tableKey;
        dataDict["_recordId"] = recordId;

        var dataJson = JsonSerializer.Serialize(dataDict, JsonOptions);

        // 发起审批
        var businessKey = $"{tableKey}:{recordId}";
        var startRequest = new ApprovalStartRequest
        {
            DefinitionId = table.ApprovalFlowDefinitionId.Value,
            BusinessKey = businessKey,
            DataJson = dataJson
        };

        var instanceResponse = await _approvalRuntimeService.StartAsync(tenantId, startRequest, userId, cancellationToken);

        // 更新记录状态字段为"审批中"
        var statusFieldValue = new DynamicFieldValueDto
        {
            Field = table.ApprovalStatusField,
            ValueType = "String",
            StringValue = "审批中"
        };
        var updateRequest = new DynamicRecordUpsertRequest(new[] { statusFieldValue });
        await _recordRepository.UpdateAsync(tenantId, table, fields, recordId, updateRequest, cancellationToken);

        return new DynamicTableApprovalSubmitResponse(
            instanceResponse.Id.ToString(),
            recordId.ToString(),
            "审批中");
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

    private IReadOnlyList<DynamicField> BuildAddFields(
        TenantId tenantId,
        long tableId,
        IReadOnlyList<DynamicFieldDefinition> definitions,
        IReadOnlyList<DynamicField> existingFields,
        DateTimeOffset now)
    {
        var fields = new List<DynamicField>(definitions.Count);
        var fallbackOrder = existingFields.Count == 0 ? 0 : existingFields.Max(x => x.SortOrder);
        foreach (var definition in definitions)
        {
            var fieldType = DynamicEnumMapper.ParseFieldType(definition.FieldType);
            var field = new DynamicField(
                tenantId,
                tableId,
                definition.Name,
                definition.DisplayName ?? definition.Name,
                fieldType,
                definition.Length,
                definition.Precision,
                definition.Scale,
                definition.AllowNull,
                definition.IsPrimaryKey,
                definition.IsAutoIncrement,
                definition.IsUnique,
                definition.DefaultValue,
                definition.SortOrder > 0 ? definition.SortOrder : ++fallbackOrder,
                _idGeneratorAccessor.NextId(),
                now);
            fields.Add(field);
        }

        return fields;
    }

    private static IReadOnlyList<DynamicField> BuildPreviewFields(
        TenantId tenantId,
        long tableId,
        IReadOnlyList<DynamicFieldDefinition> definitions,
        IReadOnlyList<DynamicField> existingFields)
    {
        var fields = new List<DynamicField>(definitions.Count);
        var fallbackOrder = existingFields.Count == 0 ? 0 : existingFields.Max(x => x.SortOrder);
        foreach (var definition in definitions)
        {
            var fieldType = DynamicEnumMapper.ParseFieldType(definition.FieldType);
            fields.Add(new DynamicField(
                tenantId,
                tableId,
                definition.Name,
                definition.DisplayName ?? definition.Name,
                fieldType,
                definition.Length,
                definition.Precision,
                definition.Scale,
                definition.AllowNull,
                definition.IsPrimaryKey,
                definition.IsAutoIncrement,
                definition.IsUnique,
                definition.DefaultValue,
                definition.SortOrder > 0 ? definition.SortOrder : ++fallbackOrder,
                id: 0,
                now: DateTimeOffset.UtcNow));
        }

        return fields;
    }

    private static void ValidateAddFieldDefinitions(
        IReadOnlyList<DynamicFieldDefinition> addFields,
        HashSet<string> existingNames)
    {
        var requestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in addFields)
        {
            if (string.IsNullOrWhiteSpace(field.Name) || !FieldNamePattern.IsMatch(field.Name) || ReservedNames.Contains(field.Name))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"字段名 '{field.Name}' 不合法。");
            }

            if (existingNames.Contains(field.Name) || !requestNames.Add(field.Name))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"字段 '{field.Name}' 已存在。");
            }

            if (field.IsPrimaryKey || field.IsAutoIncrement)
            {
                throw new BusinessException(ErrorCodes.ValidationError, "当前版本不支持在变更中新增主键或自增字段。");
            }

            var fieldType = field.FieldType?.Trim();
            if (fieldType is null)
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"字段 '{field.Name}' 类型不能为空。");
            }

            if (fieldType.Equals("String", StringComparison.OrdinalIgnoreCase) &&
                (field.Length is null || field.Length <= 0 || field.Length > 4000))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"字段 '{field.Name}' 长度必须在 1-4000 之间。");
            }

            if (fieldType.Equals("Decimal", StringComparison.OrdinalIgnoreCase))
            {
                if (field.Precision is null || field.Precision <= 0 || field.Precision > 38 ||
                    field.Scale is null || field.Scale < 0 || field.Scale > 18 ||
                    field.Scale > field.Precision)
                {
                    throw new BusinessException(ErrorCodes.ValidationError, $"字段 '{field.Name}' 精度/小数位配置不合法。");
                }
            }

            if (!field.AllowNull && string.IsNullOrWhiteSpace(field.DefaultValue))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"新增非空字段 '{field.Name}' 必须提供默认值。");
            }
        }
    }

    private static IReadOnlyList<string> BuildAddFieldSqlScripts(DynamicTable table, IReadOnlyList<DynamicField> newFields)
    {
        var scripts = new List<string>(newFields.Count * 2);
        foreach (var field in newFields)
        {
            scripts.Add(DynamicSqlBuilder.BuildAddColumnSql(table, field));
            if (field.IsUnique)
            {
                var indexName = $"uk_{table.TableKey}_{field.Name}".ToLowerInvariant();
                scripts.Add(DynamicSqlBuilder.BuildCreateIndexSql(table, new[] { field.Name }, indexName, true));
            }
        }

        return scripts;
    }

    private static IReadOnlyList<DynamicField> BuildUpdatedFields(
        IReadOnlyList<DynamicFieldUpdateDefinition> updateFields,
        IReadOnlyList<DynamicField> existingFields,
        DateTimeOffset now)
    {
        if (updateFields.Count == 0)
        {
            return Array.Empty<DynamicField>();
        }

        var map = existingFields.ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
        var updated = new List<DynamicField>(updateFields.Count);
        foreach (var update in updateFields)
        {
            if (!map.TryGetValue(update.Name, out var field))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"字段 '{update.Name}' 不存在。");
            }

            if (update.Length.HasValue || update.Precision.HasValue || update.Scale.HasValue ||
                update.AllowNull.HasValue || update.IsUnique.HasValue || update.DefaultValue is not null)
            {
                throw new BusinessException(ErrorCodes.ValidationError, "当前版本仅支持更新字段显示名和排序。");
            }

            var displayName = string.IsNullOrWhiteSpace(update.DisplayName) ? field.DisplayName : update.DisplayName;
            var sortOrder = update.SortOrder ?? field.SortOrder;
            field.Update(
                displayName,
                field.Length,
                field.Precision,
                field.Scale,
                field.AllowNull,
                field.IsUnique,
                field.DefaultValue,
                sortOrder,
                now);
            updated.Add(field);
        }

        return updated;
    }

    /// <summary>
    /// 构建预览用的已更新字段列表，创建新实例而非修改原始实体，避免污染 ORM 跟踪的领域对象。
    /// </summary>
    private static IReadOnlyList<DynamicField> BuildPreviewUpdatedFields(
        IReadOnlyList<DynamicFieldUpdateDefinition> updateFields,
        IReadOnlyList<DynamicField> existingFields,
        DateTimeOffset now)
    {
        if (updateFields.Count == 0)
        {
            return Array.Empty<DynamicField>();
        }

        var map = existingFields.ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);
        var updated = new List<DynamicField>(updateFields.Count);
        foreach (var update in updateFields)
        {
            if (!map.TryGetValue(update.Name, out var field))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"字段 '{update.Name}' 不存在。");
            }

            if (update.Length.HasValue || update.Precision.HasValue || update.Scale.HasValue ||
                update.AllowNull.HasValue || update.IsUnique.HasValue || update.DefaultValue is not null)
            {
                throw new BusinessException(ErrorCodes.ValidationError, "当前版本仅支持更新字段显示名和排序。");
            }

            var displayName = string.IsNullOrWhiteSpace(update.DisplayName) ? field.DisplayName : update.DisplayName;
            var sortOrder = update.SortOrder ?? field.SortOrder;
            updated.Add(new DynamicField(
                field.TenantId,
                field.TableId,
                field.Name,
                displayName,
                field.FieldType,
                field.Length,
                field.Precision,
                field.Scale,
                field.AllowNull,
                field.IsPrimaryKey,
                field.IsAutoIncrement,
                field.IsUnique,
                field.DefaultValue,
                sortOrder,
                field.Id,
                now));
        }

        return updated;
    }

    private static string ResolveOperationType(int addCount, int updateCount)
    {
        if (addCount > 0 && updateCount > 0)
        {
            return "ADD_UPDATE_FIELDS";
        }

        if (addCount > 0)
        {
            return "ADD_FIELDS";
        }

        if (updateCount > 0)
        {
            return "UPDATE_FIELDS_META";
        }

        return "NOOP";
    }

    private static IReadOnlyList<string> BuildMigrationSqlScripts(
        IReadOnlyList<string> ddlScripts,
        IReadOnlyList<DynamicField> updatedFields)
    {
        var scripts = new List<string>(ddlScripts.Count + updatedFields.Count);
        scripts.AddRange(ddlScripts);
        foreach (var field in updatedFields)
        {
            scripts.Add($"-- UPDATE FIELD META: {field.Name}, displayName={field.DisplayName}, sortOrder={field.SortOrder}");
        }

        return scripts;
    }

    private DynamicSchemaMigration BuildMigrationRecord(
        TenantId tenantId,
        DynamicTable table,
        string operationType,
        IReadOnlyList<string> scripts,
        long userId,
        DateTimeOffset now)
    {
        var appliedSql = string.Join(Environment.NewLine, scripts);
        return new DynamicSchemaMigration(
            tenantId,
            table.Id,
            table.TableKey,
            operationType,
            appliedSql,
            "当前版本不支持自动回滚，请通过备份恢复。",
            "Succeeded",
            userId,
            _idGeneratorAccessor.NextId(),
            now);
    }
}
