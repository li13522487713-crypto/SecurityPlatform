using Atlas.Application.DynamicTables;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Enums;
using Atlas.Infrastructure.Services;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicRecordCommandService : IDynamicRecordCommandService
{
    private readonly IDynamicTableRepository _tableRepository;
    private readonly IDynamicFieldRepository _fieldRepository;
    private readonly IDynamicRecordRepository _recordRepository;
    private readonly IDynamicRelationRepository _relationRepository;
    private readonly IRollupCalculationService _rollupService;
    private readonly IFieldPermissionResolver _fieldPermissionResolver;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly IDynamicFormValidationService _formValidationService;
    private readonly IAppDbScopeFactory _appDbScopeFactory;

    public DynamicRecordCommandService(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicRecordRepository recordRepository,
        IDynamicRelationRepository relationRepository,
        IRollupCalculationService rollupService,
        IFieldPermissionResolver fieldPermissionResolver,
        ICurrentUserAccessor currentUserAccessor,
        IAppContextAccessor appContextAccessor,
        IDynamicFormValidationService formValidationService,
        IAppDbScopeFactory appDbScopeFactory)
    {
        _tableRepository = tableRepository;
        _fieldRepository = fieldRepository;
        _recordRepository = recordRepository;
        _relationRepository = relationRepository;
        _rollupService = rollupService;
        _fieldPermissionResolver = fieldPermissionResolver;
        _currentUserAccessor = currentUserAccessor;
        _appContextAccessor = appContextAccessor;
        _formValidationService = formValidationService;
        _appDbScopeFactory = appDbScopeFactory;
    }

    public DynamicRecordCommandService(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicRecordRepository recordRepository,
        IDynamicRelationRepository relationRepository,
        IRollupCalculationService rollupService,
        IFieldPermissionResolver fieldPermissionResolver,
        ICurrentUserAccessor currentUserAccessor,
        IAppContextAccessor appContextAccessor,
        IDynamicFormValidationService formValidationService,
        ISqlSugarClient db)
        : this(
            tableRepository,
            fieldRepository,
            recordRepository,
            relationRepository,
            rollupService,
            fieldPermissionResolver,
            currentUserAccessor,
            appContextAccessor,
            formValidationService,
            new MainOnlyAppDbScopeFactory(db))
    {
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTableNotFound");
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        if (fields.Count == 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicTableFieldsEmpty");
        }

        await EnsureEditableAsync(tenantId, tableKey, table.AppId, request, cancellationToken);
        var payload = BuildPayloadDict(request.Values);
        if (!await _formValidationService.ValidateAsync(tableKey, payload, cancellationToken))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicRecordValidationFailed");
        }
        var recordId = await _recordRepository.InsertAsync(tenantId, table, fields, request, cancellationToken);

        // MasterDetail 关系写入后同步触发汇总计算
        await TriggerMasterDetailRollupAsync(tenantId, tableKey, table.Id, recordId, request, cancellationToken);

        return recordId;
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        long id,
        DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTableNotFound");
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        if (fields.Count == 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicTableFieldsEmpty");
        }

        await EnsureEditableAsync(tenantId, tableKey, table.AppId, request, cancellationToken);
        var payload = BuildPayloadDict(request.Values);
        if (!await _formValidationService.ValidateAsync(tableKey, payload, cancellationToken))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicRecordValidationFailed");
        }
        await _recordRepository.UpdateAsync(tenantId, table, fields, id, request, cancellationToken);

        // MasterDetail 关系写入后同步触发汇总计算
        await TriggerMasterDetailRollupAsync(tenantId, tableKey, table.Id, id, request, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        long id,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            return;
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        if (fields.Count == 0)
        {
            return;
        }

        // 获取主表关系定义，用于处理子记录的删除行为
        var relations = await _relationRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        if (relations.Count > 0)
        {
            await HandleChildRecordsOnDeleteAsync(tenantId, table.TableKey, id, relations, cancellationToken);
        }

        await _recordRepository.DeleteAsync(tenantId, table, fields, id, cancellationToken);
    }

    public async Task DeleteBatchAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        IReadOnlyList<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return;
        }

        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            return;
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        if (fields.Count == 0)
        {
            return;
        }

        await _recordRepository.DeleteBatchAsync(tenantId, table, fields, ids, cancellationToken);
    }

    private async Task EnsureEditableAsync(
        TenantId tenantId,
        string tableKey,
        long? appId,
        DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            throw new BusinessException(ErrorCodes.Unauthorized, "Unauthorized");
        }

        var fieldsToEdit = request.Values
            .Select(x => x.Field)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        await _fieldPermissionResolver.EnsureEditableFieldsAsync(
            tenantId,
            currentUser.UserId,
            tableKey,
            appId,
            fieldsToEdit,
            cancellationToken);
    }

    private static Dictionary<string, object> BuildPayloadDict(IReadOnlyList<DynamicFieldValueDto> values)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var dto in values)
        {
            if (string.IsNullOrWhiteSpace(dto.Field))
                continue;

            object? raw = dto.ValueType switch
            {
                "String" or "Text" or "Json" or "File" or "Image" or "Guid" or "Enum" => dto.StringValue,
                "Int" => dto.IntValue,
                "Long" => dto.LongValue,
                "Decimal" => dto.DecimalValue,
                "Bool" => dto.BoolValue,
                "DateTime" => dto.DateTimeValue,
                "Date" => dto.DateValue,
                _ => dto.StringValue
            };

            if (raw is not null)
                dict[dto.Field] = raw;
        }
        return dict;
    }

    /// <summary>
    /// MasterDetail 写入后，查找所有关联主表并同步触发汇总计算。
    /// </summary>
    private async Task TriggerMasterDetailRollupAsync(
        TenantId tenantId,
        string childTableKey,
        long childTableId,
        long childRecordId,
        DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var appId = _appContextAccessor.ResolveAppId();
        if (!appId.HasValue || appId.Value <= 0)
        {
            return;
        }

        var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);

        // 查找以本表为子表（RelatedTableKey == childTableKey）且 EnableRollup 的关系
        // 注意：此处需要查询哪些主表引用了本表，需通过 SQL 查询
        var masterRelations = await appDb.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicRelation>()
            .Where(r =>
                r.TenantIdValue == tenantId.Value
                && r.RelatedTableKey == childTableKey
                && r.EnableRollup
                && r.RelationType == "MasterDetail")
            .ToListAsync(cancellationToken);

        if (masterRelations.Count == 0)
        {
            return;
        }

        // 从请求体中找到外键值（用于定位主记录 ID）
        foreach (var relation in masterRelations)
        {
            var fkValue = request.Values
                .FirstOrDefault(v => string.Equals(v.Field, relation.TargetField, StringComparison.OrdinalIgnoreCase));
            if (fkValue is null)
            {
                continue;
            }

            long masterRecordId = 0;
            if (fkValue.LongValue.HasValue)
            {
                masterRecordId = fkValue.LongValue.Value;
            }
            else if (long.TryParse(fkValue.StringValue, out var parsedId))
            {
                masterRecordId = parsedId;
            }

            if (masterRecordId <= 0)
            {
                continue;
            }

            var masterTable = await appDb.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>()
                .Where(t => t.TenantIdValue == tenantId.Value && t.Id == relation.TableId)
                .FirstAsync(cancellationToken);
            if (masterTable is null)
            {
                continue;
            }

            await _rollupService.RecalculateAsync(tenantId, masterTable.TableKey, masterRecordId, cancellationToken);
        }
    }

    /// <summary>
    /// 批量处理子记录的删除行为（不在循环内执行 DB 操作，使用 IN 查询聚合）。
    /// </summary>
    private async Task HandleChildRecordsOnDeleteAsync(        TenantId tenantId,
        string masterTableKey,
        long masterRecordId,
        IReadOnlyList<Atlas.Domain.DynamicTables.Entities.DynamicRelation> relations,
        CancellationToken cancellationToken)
    {
        var appId = _appContextAccessor.ResolveAppId();
        if (!appId.HasValue || appId.Value <= 0)
        {
            throw new BusinessException(ErrorCodes.AppContextRequired, "缺少应用上下文，无法执行应用级联删除。");
        }
        var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);

        foreach (var relation in relations)
        {
            if (relation.OnDeleteAction == RelationOnDeleteAction.NoAction)
            {
                continue;
            }

            var childTable = await _tableRepository.FindByKeyAsync(
                tenantId, relation.RelatedTableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
            if (childTable is null)
            {
                continue;
            }

            var childTableName = childTable.TableKey;
            var foreignKeyField = relation.TargetField;

            switch (relation.OnDeleteAction)
            {
                case RelationOnDeleteAction.Restrict:
                    var childCount = await appDb.Ado.GetIntAsync(
                        $"SELECT COUNT(1) FROM \"{childTableName}\" WHERE \"{foreignKeyField}\" = @masterId",
                        new { masterId = masterRecordId });
                    if (childCount > 0)
                    {
                        throw new BusinessException(
                            ErrorCodes.ValidationError,
                            $"删除失败：关联表 {childTableName} 中存在 {childCount} 条子记录，请先删除子记录再删除主记录。");
                    }
                    break;

                case RelationOnDeleteAction.Cascade:
                    await appDb.Ado.ExecuteCommandAsync(
                        $"DELETE FROM \"{childTableName}\" WHERE \"{foreignKeyField}\" = @masterId",
                        new { masterId = masterRecordId });
                    break;

                case RelationOnDeleteAction.SetNull:
                    await appDb.Ado.ExecuteCommandAsync(
                        $"UPDATE \"{childTableName}\" SET \"{foreignKeyField}\" = NULL WHERE \"{foreignKeyField}\" = @masterId",
                        new { masterId = masterRecordId });
                    break;
            }
        }
    }
}
