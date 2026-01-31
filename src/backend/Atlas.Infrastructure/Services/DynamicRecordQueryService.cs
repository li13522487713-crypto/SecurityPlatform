using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicRecordQueryService : IDynamicRecordQueryService
{
    private readonly IDynamicTableRepository _tableRepository;
    private readonly IDynamicFieldRepository _fieldRepository;
    private readonly IDynamicRecordRepository _recordRepository;

    public DynamicRecordQueryService(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicRecordRepository recordRepository)
    {
        _tableRepository = tableRepository;
        _fieldRepository = fieldRepository;
        _recordRepository = recordRepository;
    }

    public async Task<DynamicRecordListResult> QueryAsync(
        TenantId tenantId,
        string tableKey,
        DynamicRecordQueryRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "动态表不存在。");
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        if (fields.Count == 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "动态表字段为空。");
        }

        return await _recordRepository.QueryAsync(tenantId, table, fields, request, cancellationToken);
    }

    public async Task<DynamicRecordDto?> GetByIdAsync(
        TenantId tenantId,
        string tableKey,
        long id,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, cancellationToken);
        if (table is null)
        {
            return null;
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        if (fields.Count == 0)
        {
            return null;
        }

        return await _recordRepository.GetByIdAsync(tenantId, table, fields, id, cancellationToken);
    }
}
