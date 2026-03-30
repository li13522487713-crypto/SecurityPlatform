using System.Text.Json;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Models;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicTables.Enums;
using Atlas.Infrastructure.DynamicTables;
using Atlas.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
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
    private static readonly HashSet<string> ProtectedFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
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
    private readonly IAppDbScopeFactory _appDbScopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly IApprovalRuntimeCommandService? _approvalRuntimeService;

    [ActivatorUtilitiesConstructor]
    public DynamicTableCommandService(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicIndexRepository indexRepository,
        IDynamicRelationRepository relationRepository,
        IFieldPermissionRepository fieldPermissionRepository,
        IDynamicRecordRepository recordRepository,
        IDynamicSchemaMigrationRepository? migrationRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IAppDbScopeFactory appDbScopeFactory,
        TimeProvider timeProvider,
        IAppContextAccessor appContextAccessor,
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
        _appDbScopeFactory = appDbScopeFactory;
        _timeProvider = timeProvider;
        _appContextAccessor = appContextAccessor;
        _approvalRuntimeService = approvalRuntimeService;
    }

    public DynamicTableCommandService(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicIndexRepository indexRepository,
        IDynamicRelationRepository relationRepository,
        IFieldPermissionRepository fieldPermissionRepository,
        IDynamicRecordRepository recordRepository,
        IDynamicSchemaMigrationRepository? migrationRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        SqlSugarClient db,
        TimeProvider timeProvider,
        IAppContextAccessor appContextAccessor,
        IApprovalRuntimeCommandService? approvalRuntimeService = null)
        : this(
            tableRepository,
            fieldRepository,
            indexRepository,
            relationRepository,
            fieldPermissionRepository,
            recordRepository,
            migrationRepository,
            idGeneratorAccessor,
            new MainOnlyAppDbScopeFactory(db),
            timeProvider,
            appContextAccessor,
            approvalRuntimeService)
    {
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        DynamicTableCreateRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedTableKey = request.TableKey.Trim();
        var appId = ResolveTargetAppId(request.AppId);
        var existing = await _tableRepository.FindByKeyAsync(tenantId, normalizedTableKey, appId, cancellationToken);
        if (existing is not null)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicTableKeyExists");
        }

        var dbType = DynamicEnumMapper.ParseDbType(request.DbType);
        if (dbType != DynamicDbType.Sqlite)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicTableOnlySqlite");
        }
        var now = _timeProvider.GetUtcNow();
        var table = new DynamicTable(
            tenantId,
            normalizedTableKey,
            request.DisplayName,
            request.Description,
            dbType,
            userId,
            _idGeneratorAccessor.NextId(),
            now);
        table.BindAppScope(appId, userId, now);

        var fields = BuildFields(tenantId, table.Id, request.Fields, now);
        var indexes = BuildIndexes(tenantId, table.Id, request.Indexes, now);

        var createColumns = DynamicSqlBuilder.BuildCreateTableColumns(fields);
        var indexSpecs = DynamicSqlBuilder.BuildCreateIndexSpecs(table, indexes);
        var appDb = await ResolveAppDbAsync(tenantId, appId, cancellationToken);

        var result = await appDb.Ado.UseTranAsync(async () =>
        {
            appDb.DbMaintenance.CreateTable(table.TableKey, createColumns, true);
            foreach (var indexSpec in indexSpecs)
            {
                if (appDb.DbMaintenance.IsAnyIndex(indexSpec.IndexName))
                {
                    continue;
                }

                appDb.DbMaintenance.CreateIndex(indexSpec.IndexName, indexSpec.Fields.ToArray(), table.TableKey, indexSpec.IsUnique);
            }
            await _tableRepository.AddAsync(table, cancellationToken);
            await _fieldRepository.AddRangeAsync(fields, cancellationToken);
            await _indexRepository.AddRangeAsync(indexes, cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException(ErrorCodes.ServerError, "DynamicTableCreateFailed");
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
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTableNotFound");
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
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTableNotFound");
        }

        if (table.DbType != DynamicDbType.Sqlite)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicTableOnlySqliteAlter");
        }

        if (request.AddFields.Count == 0 && request.UpdateFields.Count == 0 && request.RemoveFields.Count == 0)
        {
            return;
        }

        var existingFields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var existingNames = existingFields
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ValidateAddFieldDefinitions(request.AddFields, existingNames);
        var fieldsToRemove = BuildRemovedFields(request.RemoveFields, existingFields);
        var now = _timeProvider.GetUtcNow();
        var newFields = BuildAddFields(tenantId, table.Id, request.AddFields, existingFields, now);
        var fieldsToUpdate = BuildUpdatedFields(request.UpdateFields, existingFields, now);
        var existingIndexes = await _indexRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var indexesToDrop = BuildIndexesToDrop(fieldsToRemove, existingIndexes);
        var addColumnOperations = new List<DynamicField>(newFields.Count);
        var generatedIndexes = new List<DynamicIndex>();
        foreach (var field in newFields)
        {
            addColumnOperations.Add(field);

            if (field.IsUnique)
            {
                var indexName = $"uk_{table.TableKey}_{field.Name}".ToLowerInvariant();
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
            ResolveOperationType(newFields.Count, fieldsToUpdate.Count, fieldsToRemove.Count),
            BuildMigrationOperationLogs(newFields, fieldsToUpdate, fieldsToRemove),
            userId,
            now);
        var appDb = await ResolveAppDbAsync(tenantId, table.AppId, cancellationToken);
        var result = await appDb.Ado.UseTranAsync(async () =>
        {
            ExecuteDropIndexesSql(indexesToDrop, appDb);
            ExecuteDropColumnsSql(table.TableKey, fieldsToRemove, appDb);

            foreach (var field in addColumnOperations)
            {
                appDb.DbMaintenance.AddColumn(table.TableKey, DynamicSqlBuilder.BuildAddColumnInfo(field));
            }

            foreach (var index in generatedIndexes)
            {
                if (appDb.DbMaintenance.IsAnyIndex(index.Name))
                {
                    continue;
                }

                var fields = JsonSerializer.Deserialize<string[]>(index.FieldsJson, JsonOptions) ?? Array.Empty<string>();
                if (fields.Length == 0)
                {
                    continue;
                }

                appDb.DbMaintenance.CreateIndex(index.Name, fields, table.TableKey, index.IsUnique);
            }

            await _fieldRepository.AddRangeAsync(newFields, cancellationToken);
            await _fieldRepository.UpdateRangeAsync(fieldsToUpdate, cancellationToken);
            await _indexRepository.AddRangeAsync(generatedIndexes, cancellationToken);
            await DeleteRemovedMetadataAsync(tenantId, table, fieldsToRemove, indexesToDrop, appDb, cancellationToken);
            await _tableRepository.UpdateAsync(table, cancellationToken);
            if (_migrationRepository is not null)
            {
                await _migrationRepository.AddAsync(migrationRecord, cancellationToken);
            }
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException(ErrorCodes.ServerError, "DynamicTableAlterFailed");
        }
    }

    public async Task<DynamicTableAlterPreviewResponse> PreviewAlterAsync(
        TenantId tenantId,
        string tableKey,
        DynamicTableAlterRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTableNotFound");
        }

        if (table.DbType != DynamicDbType.Sqlite)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicTableOnlySqlitePreview");
        }

        if (request.AddFields.Count == 0 && request.UpdateFields.Count == 0 && request.RemoveFields.Count == 0)
        {
            return new DynamicTableAlterPreviewResponse(tableKey, "NOOP", Array.Empty<string>(), "未检测到可执行变更。");
        }

        var existingFields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var existingNames = existingFields
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        ValidateAddFieldDefinitions(request.AddFields, existingNames);
        var removeFields = BuildRemovedFields(request.RemoveFields, existingFields);

        var previewFields = BuildPreviewFields(tenantId, table.Id, request.AddFields, existingFields);
        var previewUpdatedFields = BuildPreviewUpdatedFields(request.UpdateFields, existingFields, DateTimeOffset.UtcNow);
        var operationLogs = BuildMigrationOperationLogs(previewFields, previewUpdatedFields, removeFields);
        return new DynamicTableAlterPreviewResponse(
            tableKey,
            ResolveOperationType(previewFields.Count, previewUpdatedFields.Count, removeFields.Count),
            operationLogs,
            "当前版本不支持自动回滚，请通过备份恢复。");
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            return;
        }

        var appDb = await ResolveAppDbAsync(tenantId, table.AppId, cancellationToken);
        var result = await appDb.Ado.UseTranAsync(async () =>
        {
            appDb.DbMaintenance.DropTable(table.TableKey);
            await _fieldRepository.DeleteByTableIdAsync(tenantId, table.Id, cancellationToken);
            await _indexRepository.DeleteByTableIdAsync(tenantId, table.Id, cancellationToken);
            await _relationRepository.DeleteByTableIdAsync(tenantId, table.Id, cancellationToken);
            await _fieldPermissionRepository.ReplaceByTableKeyAsync(tenantId, tableKey, table.AppId, Array.Empty<FieldPermission>(), cancellationToken);
            await _tableRepository.DeleteAsync(tenantId, table.Id, cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException(ErrorCodes.ServerError, "DynamicTableDeleteFailed");
        }
    }

    public async Task SetRelationsAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRelationUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTableNotFound");
        }

        var relations = request.Relations ?? Array.Empty<DynamicRelationDefinition>();
        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var sourceFieldSet = fields.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var relatedTableKeys = relations
            .Select(x => x.RelatedTableKey)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var relatedTables = await _tableRepository.QueryByKeysAsync(tenantId, relatedTableKeys, _appContextAccessor.ResolveAppId(), cancellationToken);
        var relatedTableMap = relatedTables.ToDictionary(x => x.TableKey, StringComparer.OrdinalIgnoreCase);
        if (relatedTableMap.Count != relatedTableKeys.Length)
        {
            var missing = relatedTableKeys.Where(x => !relatedTableMap.ContainsKey(x)).ToArray();
            throw new BusinessException(ErrorCodes.ValidationError, $"Related tables not found: {string.Join(", ", missing)}");
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
                throw new BusinessException(ErrorCodes.ValidationError, $"Source field {relation.SourceField} does not exist.");
            }

            var relatedTable = relatedTableMap[relation.RelatedTableKey];
            if (!relatedFieldMap.TryGetValue(relatedTable.Id, out var targetSet))
            {
                targetSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            if (!targetSet.Contains(relation.TargetField))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"Related field {relation.TargetField} does not exist in table {relation.RelatedTableKey}.");
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
                now,
                ParseMultiplicity(relation.Multiplicity),
                ParseOnDeleteAction(relation.OnDeleteAction),
                relation.EnableRollup,
                relation.RollupDefinitionsJson));
        }

        var appDb = await ResolveAppDbAsync(tenantId, table.AppId, cancellationToken);
        var tran = await appDb.Ado.UseTranAsync(async () =>
        {
            await _relationRepository.DeleteByTableIdAsync(tenantId, table.Id, cancellationToken);
            await _relationRepository.AddRangeAsync(relationEntities, cancellationToken);
        });

        if (!tran.IsSuccess)
        {
            throw tran.ErrorException ?? new BusinessException(ErrorCodes.ServerError, "DynamicTableRelationsUpdateFailed");
        }
    }

    public async Task SetFieldPermissionsAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicFieldPermissionUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTableNotFound");
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
            throw new BusinessException(ErrorCodes.ValidationError, $"Fields not found: {string.Join(", ", invalid)}");
        }

        var now = _timeProvider.GetUtcNow();
        var scopedTableKey = BuildFieldPermissionTableKey(tableKey, table.AppId);
        var entities = rules.Select(x => new FieldPermission(
            tenantId,
            scopedTableKey,
            x.FieldName,
            x.RoleCode,
            x.CanView,
            x.CanEdit,
            _idGeneratorAccessor.NextId(),
            now)).ToArray();

        await _fieldPermissionRepository.ReplaceByTableKeyAsync(tenantId, tableKey, table.AppId, entities, cancellationToken);
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
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTableNotFound");
        }

        var now = _timeProvider.GetUtcNow();

        if (request.ApprovalFlowDefinitionId.HasValue && !string.IsNullOrWhiteSpace(request.ApprovalStatusField))
        {
            // 验证状态字段是否存在于动态表中
            var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
            var hasField = fields.Any(f => f.Name.Equals(request.ApprovalStatusField, StringComparison.OrdinalIgnoreCase));
            if (!hasField)
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"Approval status field '{request.ApprovalStatusField}' does not exist in table '{tableKey}'.");
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
            throw new BusinessException(ErrorCodes.ServerError, "DynamicTableApprovalServiceNotAvailable");
        }

        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTableNotFound");
        }

        if (!table.ApprovalFlowDefinitionId.HasValue || string.IsNullOrWhiteSpace(table.ApprovalStatusField))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicTableNotBoundApprovalFlow");
        }

        // 读取记录数据，构建 DataJson
        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        var record = await _recordRepository.GetByIdAsync(tenantId, table, fields, recordId, cancellationToken);
        if (record is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicRecordNotFound");
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
                throw new BusinessException(ErrorCodes.ValidationError, $"Field name '{field.Name}' is invalid.");
            }

            if (existingNames.Contains(field.Name) || !requestNames.Add(field.Name))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"Field '{field.Name}' already exists.");
            }

            if (field.IsPrimaryKey || field.IsAutoIncrement)
            {
                throw new BusinessException(ErrorCodes.ValidationError, "DynamicTablePrimaryKeyNotSupported");
            }

            var fieldType = field.FieldType?.Trim();
            if (fieldType is null)
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"Field '{field.Name}' type is required.");
            }

            if (fieldType.Equals("String", StringComparison.OrdinalIgnoreCase) &&
                (field.Length is null || field.Length <= 0 || field.Length > 4000))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"Field '{field.Name}' length must be between 1 and 4000.");
            }

            if (fieldType.Equals("Decimal", StringComparison.OrdinalIgnoreCase))
            {
                if (field.Precision is null || field.Precision <= 0 || field.Precision > 38 ||
                    field.Scale is null || field.Scale < 0 || field.Scale > 18 ||
                    field.Scale > field.Precision)
                {
                    throw new BusinessException(ErrorCodes.ValidationError, $"Field '{field.Name}' precision/scale configuration is invalid.");
                }
            }

            if (!field.AllowNull && string.IsNullOrWhiteSpace(field.DefaultValue))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"Non-nullable field '{field.Name}' must have a default value.");
            }
        }
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
                throw new BusinessException(ErrorCodes.ValidationError, $"Field '{update.Name}' does not exist.");
            }

            if (update.Length.HasValue || update.Precision.HasValue || update.Scale.HasValue ||
                update.AllowNull.HasValue || update.IsUnique.HasValue || update.DefaultValue is not null)
            {
                throw new BusinessException(ErrorCodes.ValidationError, "DynamicTableFieldUpdateLimitedFields");
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
                throw new BusinessException(ErrorCodes.ValidationError, $"Field '{update.Name}' does not exist.");
            }

            if (update.Length.HasValue || update.Precision.HasValue || update.Scale.HasValue ||
                update.AllowNull.HasValue || update.IsUnique.HasValue || update.DefaultValue is not null)
            {
                throw new BusinessException(ErrorCodes.ValidationError, "DynamicTableFieldUpdateLimitedFields");
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

    private static string ResolveOperationType(int addCount, int updateCount, int removeCount)
    {
        if (addCount > 0 && updateCount > 0 && removeCount > 0)
        {
            return "ADD_UPDATE_REMOVE_FIELDS";
        }

        if (addCount > 0 && updateCount > 0)
        {
            return "ADD_UPDATE_FIELDS";
        }

        if (addCount > 0 && removeCount > 0)
        {
            return "ADD_REMOVE_FIELDS";
        }

        if (updateCount > 0 && removeCount > 0)
        {
            return "UPDATE_REMOVE_FIELDS";
        }

        if (addCount > 0)
        {
            return "ADD_FIELDS";
        }

        if (updateCount > 0)
        {
            return "UPDATE_FIELDS_META";
        }

        if (removeCount > 0)
        {
            return "REMOVE_FIELDS";
        }

        return "NOOP";
    }

    private static IReadOnlyList<string> BuildMigrationOperationLogs(
        IReadOnlyList<DynamicField> newFields,
        IReadOnlyList<DynamicField> updatedFields,
        IReadOnlyList<DynamicField> removedFields)
    {
        var scripts = new List<string>(newFields.Count + updatedFields.Count + removedFields.Count);
        foreach (var field in newFields)
        {
            scripts.Add($"ADD COLUMN: {field.Name} ({field.FieldType})");
            if (field.IsUnique)
            {
                scripts.Add($"CREATE UNIQUE INDEX: uk_*_{field.Name}");
            }
        }

        foreach (var field in updatedFields)
        {
            scripts.Add($"-- UPDATE FIELD META: {field.Name}, displayName={field.DisplayName}, sortOrder={field.SortOrder}");
        }

        foreach (var field in removedFields)
        {
            scripts.Add($"DROP COLUMN: {field.Name}");
        }

        return scripts;
    }

    private static IReadOnlyList<DynamicField> BuildRemovedFields(
        IReadOnlyList<string> removeFieldNames,
        IReadOnlyList<DynamicField> existingFields)
    {
        if (removeFieldNames.Count == 0)
        {
            return Array.Empty<DynamicField>();
        }

        var existingMap = existingFields.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var removed = new List<DynamicField>(removeFieldNames.Count);
        foreach (var fieldName in removeFieldNames)
        {
            if (!existingMap.TryGetValue(fieldName, out var field))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"Field '{fieldName}' does not exist.");
            }

            if (field.IsPrimaryKey || field.IsAutoIncrement || ProtectedFieldNames.Contains(field.Name))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"Field '{fieldName}' cannot be deleted.");
            }

            removed.Add(field);
        }

        return removed;
    }

    private static IReadOnlyList<DynamicIndex> BuildIndexesToDrop(
        IReadOnlyList<DynamicField> removeFields,
        IReadOnlyList<DynamicIndex> existingIndexes)
    {
        if (removeFields.Count == 0 || existingIndexes.Count == 0)
        {
            return Array.Empty<DynamicIndex>();
        }

        var removeNameSet = removeFields.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return existingIndexes
            .Where(index =>
            {
                var fields = JsonSerializer.Deserialize<string[]>(index.FieldsJson, JsonOptions) ?? Array.Empty<string>();
                return fields.Any(removeNameSet.Contains);
            })
            .ToArray();
    }

    private static void ExecuteDropIndexesSql(IReadOnlyList<DynamicIndex> indexesToDrop, ISqlSugarClient db)
    {
        if (indexesToDrop.Count == 0)
        {
            return;
        }

        var sql = string.Join(
            Environment.NewLine,
            indexesToDrop.Select(index => $"DROP INDEX IF EXISTS \"{EscapeIdentifier(index.Name)}\";"));
        db.Ado.ExecuteCommand(sql);
    }

    private static void ExecuteDropColumnsSql(string tableKey, IReadOnlyList<DynamicField> fieldsToRemove, ISqlSugarClient db)
    {
        if (fieldsToRemove.Count == 0)
        {
            return;
        }

        var escapedTable = EscapeIdentifier(tableKey);
        var sql = string.Join(
            Environment.NewLine,
            fieldsToRemove.Select(field => $"ALTER TABLE \"{escapedTable}\" DROP COLUMN \"{EscapeIdentifier(field.Name)}\";"));
        db.Ado.ExecuteCommand(sql);
    }

    private async Task DeleteRemovedMetadataAsync(
        TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fieldsToRemove,
        IReadOnlyList<DynamicIndex> indexesToDrop,
        ISqlSugarClient db,
        CancellationToken cancellationToken)
    {
        if (fieldsToRemove.Count > 0)
        {
            var fieldIds = fieldsToRemove.Select(x => x.Id).ToArray();
            var fieldNames = fieldsToRemove.Select(x => x.Name).ToArray();
            var scopedTableKey = BuildFieldPermissionTableKey(table.TableKey, table.AppId);

            await db.Deleteable<DynamicField>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == table.Id && SqlFunc.ContainsArray(fieldIds, x.Id))
                .ExecuteCommandAsync(cancellationToken);

            await db.Deleteable<FieldPermission>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.TableKey == scopedTableKey && SqlFunc.ContainsArray(fieldNames, x.FieldName))
                .ExecuteCommandAsync(cancellationToken);

            await db.Deleteable<DynamicRelation>()
                .Where(x =>
                    x.TenantIdValue == tenantId.Value &&
                    ((x.TableId == table.Id && SqlFunc.ContainsArray(fieldNames, x.SourceField))
                     || (x.RelatedTableKey == table.TableKey && SqlFunc.ContainsArray(fieldNames, x.TargetField))))
                .ExecuteCommandAsync(cancellationToken);
        }

        if (indexesToDrop.Count > 0)
        {
            var indexIds = indexesToDrop.Select(x => x.Id).ToArray();
            await db.Deleteable<DynamicIndex>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.TableId == table.Id && SqlFunc.ContainsArray(indexIds, x.Id))
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    private static string EscapeIdentifier(string identifier)
    {
        return identifier.Replace("\"", "\"\"", StringComparison.Ordinal);
    }

    public async Task RollbackMigrationAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        long migrationId,
        CancellationToken cancellationToken)
    {
        if (_migrationRepository is null)
        {
            throw new BusinessException(ErrorCodes.ServerError, "Migration repository is not available.");
        }

        var migration = await _migrationRepository.GetByIdAsync(tenantId, migrationId, cancellationToken);
        if (migration is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, $"Migration {migrationId} not found.");
        }

        if (string.IsNullOrWhiteSpace(migration.RollbackSql) ||
            migration.RollbackSql.Contains("不支持自动回滚", StringComparison.Ordinal))
        {
            throw new BusinessException(ErrorCodes.ValidationError,
                "This migration does not have an executable rollback script. Please restore from backup.");
        }

        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, $"Table '{tableKey}' not found.");
        }

        var appDb = await ResolveAppDbAsync(tenantId, table.AppId, cancellationToken);
        await appDb.Ado.ExecuteCommandAsync(migration.RollbackSql, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var rollbackMigration = new DynamicSchemaMigration(
            tenantId,
            table.Id,
            tableKey,
            "Rollback",
            migration.RollbackSql,
            migration.AppliedSql,
            "Succeeded",
            userId,
            _idGeneratorAccessor.NextId(),
            now);
        await _migrationRepository.AddAsync(rollbackMigration, cancellationToken);
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

    private Task<ISqlSugarClient> ResolveAppDbAsync(TenantId tenantId, long? appId, CancellationToken cancellationToken)
    {
        if (!appId.HasValue || appId.Value <= 0)
        {
            throw new BusinessException(ErrorCodes.AppContextRequired, "缺少应用上下文，无法访问应用级数据库。");
        }

        return _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
    }

    private long? ResolveTargetAppId(string? requestAppId)
    {
        if (!string.IsNullOrWhiteSpace(requestAppId)
            && long.TryParse(requestAppId, out var parsed)
            && parsed > 0)
        {
            return parsed;
        }

        return _appContextAccessor.ResolveAppId();
    }

    private static string BuildFieldPermissionTableKey(string tableKey, long? appId)
    {
        return appId.HasValue ? $"app:{appId.Value}:{tableKey}" : tableKey;
    }

    private static Atlas.Domain.DynamicTables.Enums.RelationMultiplicity ParseMultiplicity(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "onetoone" or "1:1" => Atlas.Domain.DynamicTables.Enums.RelationMultiplicity.OneToOne,
            "manytomany" or "n:n" => Atlas.Domain.DynamicTables.Enums.RelationMultiplicity.ManyToMany,
            _ => Atlas.Domain.DynamicTables.Enums.RelationMultiplicity.OneToMany
        };
    }

    private static Atlas.Domain.DynamicTables.Enums.RelationOnDeleteAction ParseOnDeleteAction(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "cascade" => Atlas.Domain.DynamicTables.Enums.RelationOnDeleteAction.Cascade,
            "setnull" => Atlas.Domain.DynamicTables.Enums.RelationOnDeleteAction.SetNull,
            "restrict" => Atlas.Domain.DynamicTables.Enums.RelationOnDeleteAction.Restrict,
            _ => Atlas.Domain.DynamicTables.Enums.RelationOnDeleteAction.NoAction
        };
    }
}
