using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicRecordCommandService : IDynamicRecordCommandService
{
    private readonly IDynamicTableRepository _tableRepository;
    private readonly IDynamicFieldRepository _fieldRepository;
    private readonly IDynamicRecordRepository _recordRepository;
    private readonly IFieldPermissionResolver _fieldPermissionResolver;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAppContextAccessor _appContextAccessor;

    public DynamicRecordCommandService(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicRecordRepository recordRepository,
        IFieldPermissionResolver fieldPermissionResolver,
        ICurrentUserAccessor currentUserAccessor,
        IAppContextAccessor appContextAccessor)
    {
        _tableRepository = tableRepository;
        _fieldRepository = fieldRepository;
        _recordRepository = recordRepository;
        _fieldPermissionResolver = fieldPermissionResolver;
        _currentUserAccessor = currentUserAccessor;
        _appContextAccessor = appContextAccessor;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "动态表不存在。");
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        if (fields.Count == 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "动态表字段为空。");
        }

        await EnsureEditableAsync(tenantId, tableKey, table.AppId, request, cancellationToken);
        return await _recordRepository.InsertAsync(tenantId, table, fields, request, cancellationToken);
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        long id,
        DynamicRecordUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, ResolveAppId(), cancellationToken);
        if (table is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "动态表不存在。");
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        if (fields.Count == 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "动态表字段为空。");
        }

        await EnsureEditableAsync(tenantId, tableKey, table.AppId, request, cancellationToken);
        await _recordRepository.UpdateAsync(tenantId, table, fields, id, request, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        long id,
        CancellationToken cancellationToken)
    {
        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, ResolveAppId(), cancellationToken);
        if (table is null)
        {
            return;
        }

        var fields = await _fieldRepository.ListByTableIdAsync(tenantId, table.Id, cancellationToken);
        if (fields.Count == 0)
        {
            return;
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

        var table = await _tableRepository.FindByKeyAsync(tenantId, tableKey, ResolveAppId(), cancellationToken);
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
            throw new BusinessException(ErrorCodes.Unauthorized, "未登录。");
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

    private long? ResolveAppId()
    {
        var appIdText = _appContextAccessor.GetAppId();
        if (long.TryParse(appIdText, out var appId))
        {
            return appId;
        }

        return null;
    }
}
