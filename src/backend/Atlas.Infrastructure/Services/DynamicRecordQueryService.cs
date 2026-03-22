using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Application.Identity.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Core.Utilities;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicTables.Enums;
using System.Text.Json;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicRecordQueryService : IDynamicRecordQueryService
{
    private readonly IDynamicTableRepository _tableRepository;
    private readonly IDynamicFieldRepository _fieldRepository;
    private readonly IDynamicRecordRepository _recordRepository;
    private readonly IDynamicRelationRepository _relationRepository;
    private readonly IFieldPermissionResolver _fieldPermissionResolver;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly IDataScopeFilter _dataScopeFilter;
    private static readonly string[] OwnerFieldCandidates = ["ownerid", "createdby", "creatorid", "owner_id", "created_by"];
    private const int ExportPageSize = 1000;
    private const int MaxExportRows = 10_000;

    public DynamicRecordQueryService(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicRecordRepository recordRepository,
        IDynamicRelationRepository relationRepository,
        IFieldPermissionResolver fieldPermissionResolver,
        ICurrentUserAccessor currentUserAccessor,
        IAppContextAccessor appContextAccessor,
        IDataScopeFilter dataScopeFilter)
    {
        _tableRepository = tableRepository;
        _fieldRepository = fieldRepository;
        _recordRepository = recordRepository;
        _relationRepository = relationRepository;
        _fieldPermissionResolver = fieldPermissionResolver;
        _currentUserAccessor = currentUserAccessor;
        _appContextAccessor = appContextAccessor;
        _dataScopeFilter = dataScopeFilter;
    }

    public async Task<DynamicRecordListResult> QueryAsync(
        TenantId tenantId,
        string tableKey,
        DynamicRecordQueryRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "动态表不存在。");
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        fields = await FilterFieldsByPermissionAsync(tenantId, tableKey, table.AppId, fields, cancellationToken);
        if (fields.Count == 0)
        {
            throw new BusinessException(ErrorCodes.Forbidden, "无可访问字段。");
        }

        var ownerFilterId = await _dataScopeFilter.GetOwnerFilterIdAsync(cancellationToken);
        var effectiveRequest = request;
        if (ownerFilterId.HasValue)
        {
            var ownerField = ResolveOwnerField(fields);
            if (ownerField is null)
            {
                return BuildEmptyResult(request, fields);
            }

            effectiveRequest = AppendOwnerFilter(request, ownerField.Name, ownerFilterId.Value);
        }

        return await _recordRepository.QueryAsync(tenantId, table, fields, effectiveRequest, cancellationToken);
    }

    public async Task<DynamicRecordDto?> GetByIdAsync(
        TenantId tenantId,
        string tableKey,
        long id,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            return null;
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        fields = await FilterFieldsByPermissionAsync(tenantId, tableKey, table.AppId, fields, cancellationToken);
        if (fields.Count == 0)
        {
            return null;
        }

        var record = await _recordRepository.GetByIdAsync(tenantId, table, fields, id, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var ownerFilterId = await _dataScopeFilter.GetOwnerFilterIdAsync(cancellationToken);
        if (!ownerFilterId.HasValue)
        {
            return record;
        }

        var ownerField = ResolveOwnerField(fields);
        if (ownerField is null)
        {
            return null;
        }

        return IsOwnedBy(record, ownerField.Name, ownerFilterId.Value) ? record : null;
    }

    public async Task<DynamicRecordExportResult> ExportAsync(
        TenantId tenantId,
        string tableKey,
        DynamicRecordExportRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, _appContextAccessor.ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "动态表不存在。");
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        fields = await FilterFieldsByPermissionAsync(tenantId, tableKey, table.AppId, fields, cancellationToken);
        if (fields.Count == 0)
        {
            throw new BusinessException(ErrorCodes.Forbidden, "无可访问字段。");
        }

        var ownerFilterId = await _dataScopeFilter.GetOwnerFilterIdAsync(cancellationToken);
        var baseRequest = new DynamicRecordQueryRequest(
            1,
            ExportPageSize,
            request.Keyword,
            request.SortBy,
            request.SortDesc,
            request.Filters ?? Array.Empty<DynamicFilterCondition>());
        DynamicRecordQueryRequest effectiveRequest = baseRequest;
        if (ownerFilterId.HasValue)
        {
            var ownerField = ResolveOwnerField(fields);
            if (ownerField is null)
            {
                return new DynamicRecordExportResult(
                    $"{tableKey}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv",
                    "text/csv; charset=utf-8",
                    BuildCsv(ResolveExportFields(fields, request.Fields), Array.Empty<DynamicRecordDto>()));
            }

            effectiveRequest = AppendOwnerFilter(baseRequest, ownerField.Name, ownerFilterId.Value);
        }

        var selectedFields = ResolveExportFields(fields, request.Fields);
        var records = await FetchExportRecordsPaginatedAsync(tenantId, table, fields, effectiveRequest, cancellationToken);
        var content = BuildCsv(selectedFields, records);
        var fileName = $"{tableKey}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";
        return new DynamicRecordExportResult(fileName, "text/csv; charset=utf-8", content);
    }

    private async Task<IReadOnlyList<DynamicRecordDto>> FetchExportRecordsPaginatedAsync(
        TenantId tenantId,
        DynamicTable table,
        IReadOnlyList<DynamicField> fields,
        DynamicRecordQueryRequest baseRequest,
        CancellationToken cancellationToken)
    {
        var allRecords = new List<DynamicRecordDto>();
        var pageIndex = 1;

        while (allRecords.Count < MaxExportRows)
        {
            var request = new DynamicRecordQueryRequest(
                pageIndex,
                ExportPageSize,
                baseRequest.Keyword,
                baseRequest.SortBy,
                baseRequest.SortDesc,
                baseRequest.Filters ?? Array.Empty<DynamicFilterCondition>());

            var result = await _recordRepository.QueryAsync(tenantId, table, fields, request, cancellationToken);

            foreach (var item in result.Items)
            {
                if (allRecords.Count >= MaxExportRows)
                {
                    break;
                }

                allRecords.Add(item);
            }

            if (result.Items.Count < ExportPageSize || allRecords.Count >= MaxExportRows)
            {
                break;
            }

            pageIndex++;
        }

        return allRecords;
    }

    private static DynamicRecordListResult BuildEmptyResult(
        DynamicRecordQueryRequest request,
        IReadOnlyList<DynamicField> fields)
    {
        return new DynamicRecordListResult(
            Array.Empty<DynamicRecordDto>(),
            0,
            request.PageIndex < 1 ? 1 : request.PageIndex,
            request.PageSize < 1 ? 20 : request.PageSize,
            fields.Select(field => new DynamicColumnDef(
                field.Name,
                string.IsNullOrWhiteSpace(field.DisplayName) ? field.Name : field.DisplayName,
                field.FieldType == DynamicFieldType.Bool ? "status" : "text",
                true,
                field.FieldType is DynamicFieldType.String or DynamicFieldType.Text,
                false)).ToArray());
    }

    private static DynamicField? ResolveOwnerField(IReadOnlyList<DynamicField> fields)
    {
        return fields.FirstOrDefault(field => OwnerFieldCandidates.Contains(field.Name, StringComparer.OrdinalIgnoreCase));
    }

    private static DynamicRecordQueryRequest AppendOwnerFilter(
        DynamicRecordQueryRequest request,
        string ownerFieldName,
        long ownerId)
    {
        var filters = (request.Filters ?? Array.Empty<DynamicFilterCondition>())
            .Where(filter => !string.Equals(filter.Field, ownerFieldName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        filters.Add(new DynamicFilterCondition(
            ownerFieldName,
            "eq",
            JsonSerializer.SerializeToElement(ownerId)));

        return new DynamicRecordQueryRequest(
            request.PageIndex,
            request.PageSize,
            request.Keyword,
            request.SortBy,
            request.SortDesc,
            filters);
    }

    private static bool IsOwnedBy(DynamicRecordDto record, string ownerFieldName, long ownerId)
    {
        var ownerValue = record.Values.FirstOrDefault(x => string.Equals(x.Field, ownerFieldName, StringComparison.OrdinalIgnoreCase));
        if (ownerValue is null)
        {
            return false;
        }

        if (ownerValue.LongValue.HasValue)
        {
            return ownerValue.LongValue.Value == ownerId;
        }
        if (ownerValue.IntValue.HasValue)
        {
            return ownerValue.IntValue.Value == ownerId;
        }
        if (!string.IsNullOrWhiteSpace(ownerValue.StringValue) && long.TryParse(ownerValue.StringValue, out var parsed))
        {
            return parsed == ownerId;
        }

        return false;
    }

    private async Task<IReadOnlyList<DynamicField>> FilterFieldsByPermissionAsync(
        TenantId tenantId,
        string tableKey,
        long? appId,
        IReadOnlyList<DynamicField> fields,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return fields;
        }

        return await _fieldPermissionResolver.FilterViewableFieldsAsync(
            tenantId,
            currentUser.UserId,
            tableKey,
            appId,
            fields,
            cancellationToken);
    }

    private static IReadOnlyList<DynamicField> ResolveExportFields(
        IReadOnlyList<DynamicField> fields,
        IReadOnlyList<string>? requestedFields)
    {
        if (requestedFields is null || requestedFields.Count == 0)
        {
            return fields.OrderBy(x => x.SortOrder).ToArray();
        }

        var fieldSet = requestedFields
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = fields.Where(x => fieldSet.Contains(x.Name)).OrderBy(x => x.SortOrder).ToArray();
        return selected.Length == 0 ? fields.OrderBy(x => x.SortOrder).ToArray() : selected;
    }

    private static byte[] BuildCsv(
        IReadOnlyList<DynamicField> selectedFields,
        IReadOnlyList<DynamicRecordDto> records)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(string.Join(",", selectedFields.Select(x => CsvUtility.EscapeField(string.IsNullOrWhiteSpace(x.DisplayName) ? x.Name : x.DisplayName!))));

        foreach (var record in records)
        {
            var valueMap = record.Values.ToDictionary(x => x.Field, StringComparer.OrdinalIgnoreCase);
            var row = new List<string>(selectedFields.Count);
            foreach (var field in selectedFields)
            {
                if (!valueMap.TryGetValue(field.Name, out var value))
                {
                    row.Add(string.Empty);
                    continue;
                }

                row.Add(CsvUtility.EscapeField(ResolveCsvValue(value)));
            }

            builder.AppendLine(string.Join(",", row));
        }

        var csvContent = builder.ToString();
        return CsvUtility.GetUtf8BytesWithBom(csvContent);
    }

    /// <summary>
    /// 解析 CSV 导出时的字段值。优先使用类型化值（Int/Long/Decimal 等）以保证输出一致；
    /// 仅当无类型化值时使用 StringValue，并 Trim 去除首尾空格。
    /// </summary>
    private static string ResolveCsvValue(DynamicFieldValueDto value)
    {
        if (value.IntValue.HasValue)
        {
            return value.IntValue.Value.ToString();
        }

        if (value.LongValue.HasValue)
        {
            return value.LongValue.Value.ToString();
        }

        if (value.DecimalValue.HasValue)
        {
            return value.DecimalValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (value.BoolValue.HasValue)
        {
            return value.BoolValue.Value ? "true" : "false";
        }

        if (value.DateTimeValue.HasValue)
        {
            return value.DateTimeValue.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        if (value.DateValue.HasValue)
        {
            return value.DateValue.Value.ToString("yyyy-MM-dd");
        }

        if (!string.IsNullOrWhiteSpace(value.StringValue))
        {
            return value.StringValue.Trim();
        }

        return string.Empty;
    }

    public async Task<DynamicRecordListResult> GetRelatedRecordsAsync(
        TenantId tenantId,
        string sourceTableKey,
        long sourceRecordId,
        string relatedTableKey,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var appId = _appContextAccessor.ResolveAppId();
        var sourceTable = await _tableRepository.FindByKeyAsync(tenantId, sourceTableKey, appId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"Source table '{sourceTableKey}' not found.");

        var relations = await _relationRepository.ListByTableIdAsync(tenantId, sourceTable.Id, cancellationToken);
        var relation = relations.FirstOrDefault(r =>
            r.RelatedTableKey.Equals(relatedTableKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessException(ErrorCodes.NotFound,
                $"No relation from '{sourceTableKey}' to '{relatedTableKey}' found.");

        var sourceFields = await _fieldRepository.ListByTableIdAsync(tenantId, sourceTable.Id, cancellationToken);
        var sourceRecord = await _recordRepository.GetByIdAsync(tenantId, sourceTable, sourceFields, sourceRecordId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, $"Source record '{sourceRecordId}' not found.");

        var matchingField = sourceRecord.Values
            .FirstOrDefault(v => v.Field.Equals(relation.SourceField, StringComparison.OrdinalIgnoreCase));
        var sourceFieldValue = matchingField is not null ? ResolveCsvValue(matchingField) : null;
        if (string.IsNullOrEmpty(sourceFieldValue))
        {
            return new DynamicRecordListResult(
                Array.Empty<DynamicRecordDto>(), 0, pageIndex, pageSize, Array.Empty<DynamicColumnDef>());
        }

        var filterValue = System.Text.Json.JsonSerializer.SerializeToElement(sourceFieldValue);
        var queryRequest = new DynamicRecordQueryRequest(
            pageIndex,
            pageSize,
            null,
            null,
            false,
            new[] { new DynamicFilterCondition(relation.TargetField, "=", filterValue) });

        return await QueryAsync(tenantId, relatedTableKey, queryRequest, cancellationToken);
    }
}
