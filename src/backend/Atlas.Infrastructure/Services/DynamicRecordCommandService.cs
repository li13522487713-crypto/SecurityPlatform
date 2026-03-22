using Atlas.Application.DynamicTables;
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
    private readonly IDynamicFormValidationService _formValidationService;

    public DynamicRecordCommandService(
        IDynamicTableRepository tableRepository,
        IDynamicFieldRepository fieldRepository,
        IDynamicRecordRepository recordRepository,
        IFieldPermissionResolver fieldPermissionResolver,
        ICurrentUserAccessor currentUserAccessor,
        IAppContextAccessor appContextAccessor,
        IDynamicFormValidationService formValidationService)
    {
        _tableRepository = tableRepository;
        _fieldRepository = fieldRepository;
        _recordRepository = recordRepository;
        _fieldPermissionResolver = fieldPermissionResolver;
        _currentUserAccessor = currentUserAccessor;
        _appContextAccessor = appContextAccessor;
        _formValidationService = formValidationService;
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

}
