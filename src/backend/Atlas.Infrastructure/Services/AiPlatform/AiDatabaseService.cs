using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class AiDatabaseService : IAiDatabaseService
{
    private readonly AiDatabaseRepository _databaseRepository;
    private readonly AiDatabaseFieldRepository _fieldRepository;
    private readonly AiDatabaseChannelConfigRepository _channelConfigRepository;
    private readonly AiDatabaseRecordRepository _recordRepository;
    private readonly AiDatabaseImportTaskRepository _importTaskRepository;
    private readonly AiAppResourceBindingRepository _appResourceBindingRepository;
    private readonly AiDatabaseQuotaPolicy _quotaPolicy;
    private readonly AiDatabasePhysicalTableService _physicalTableService;
    private readonly IAiDatabaseProvisioner _provisioner;
    private readonly IFileStorageService _fileStorageService;
    private readonly IBackgroundWorkQueue _backgroundWorkQueue;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AiDatabaseService> _logger;

    public AiDatabaseService(
        AiDatabaseRepository databaseRepository,
        AiDatabaseFieldRepository fieldRepository,
        AiDatabaseChannelConfigRepository channelConfigRepository,
        AiDatabaseRecordRepository recordRepository,
        AiDatabaseImportTaskRepository importTaskRepository,
        AiAppResourceBindingRepository appResourceBindingRepository,
        AiDatabaseQuotaPolicy quotaPolicy,
        AiDatabasePhysicalTableService physicalTableService,
        IAiDatabaseProvisioner provisioner,
        IFileStorageService fileStorageService,
        IBackgroundWorkQueue backgroundWorkQueue,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork,
        ILogger<AiDatabaseService> logger)
    {
        _databaseRepository = databaseRepository;
        _fieldRepository = fieldRepository;
        _channelConfigRepository = channelConfigRepository;
        _recordRepository = recordRepository;
        _importTaskRepository = importTaskRepository;
        _appResourceBindingRepository = appResourceBindingRepository;
        _quotaPolicy = quotaPolicy;
        _physicalTableService = physicalTableService;
        _provisioner = provisioner;
        _fileStorageService = fileStorageService;
        _backgroundWorkQueue = backgroundWorkQueue;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<AiDatabaseListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        long? workspaceId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _databaseRepository.GetPagedAsync(
            tenantId,
            keyword,
            workspaceId,
            pageIndex,
            pageSize,
            cancellationToken);

        var mapped = new List<AiDatabaseListItem>(items.Count);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureStorageReadyAsync(item, cancellationToken);
            var draftCount = item.StorageMode == AiDatabaseStorageMode.Standalone ? 0 : await _physicalTableService.CountRowsAsync(item, AiDatabaseRecordEnvironment.Draft, cancellationToken);
            var onlineCount = item.StorageMode == AiDatabaseStorageMode.Standalone ? 0 : await _physicalTableService.CountRowsAsync(item, AiDatabaseRecordEnvironment.Online, cancellationToken);
            if (item.RecordCount != draftCount + onlineCount)
            {
                item.SetRecordCount(draftCount + onlineCount);
                await _databaseRepository.UpdateAsync(item, cancellationToken);
            }

            mapped.Add(MapListItem(item, draftCount, onlineCount));
        }

        return new PagedResult<AiDatabaseListItem>(mapped, total, pageIndex, pageSize);
    }

    public async Task<AiDatabaseDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _databaseRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        await EnsureStorageReadyAsync(entity, cancellationToken);
        var fields = await GetOrBootstrapFieldsAsync(entity, cancellationToken);
        var channelConfigs = await GetOrBootstrapChannelConfigsAsync(entity, cancellationToken);
        var draftCount = entity.StorageMode == AiDatabaseStorageMode.Standalone ? 0 : await _physicalTableService.CountRowsAsync(entity, AiDatabaseRecordEnvironment.Draft, cancellationToken);
        var onlineCount = entity.StorageMode == AiDatabaseStorageMode.Standalone ? 0 : await _physicalTableService.CountRowsAsync(entity, AiDatabaseRecordEnvironment.Online, cancellationToken);
        if (entity.RecordCount != draftCount + onlineCount)
        {
            entity.SetRecordCount(draftCount + onlineCount);
            await _databaseRepository.UpdateAsync(entity, cancellationToken);
        }

        return MapDetail(entity, fields, channelConfigs, draftCount, onlineCount);
    }

    public async Task<long> CreateAsync(TenantId tenantId, AiDatabaseCreateRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();
        if (await _databaseRepository.ExistsByNameAsync(tenantId, normalizedName, excludeId: null, cancellationToken))
        {
            throw new BusinessException("数据库名称已存在。", ErrorCodes.ValidationError);
        }

        var normalizedFields = NormalizeFields(request.TableSchema, request.Fields);
        var schemaJson = SerializeSchema(normalizedFields);
        var validate = await ValidateSchemaAsync(schemaJson, cancellationToken);
        if (!validate.IsValid)
        {
            throw new BusinessException("数据库 Schema 不合法。", ErrorCodes.ValidationError);
        }

        await _quotaPolicy.EnsureCanCreateDatabaseAsync(tenantId, cancellationToken);
        _quotaPolicy.EnsureFieldCount(normalizedFields.Count);

        var id = _idGeneratorAccessor.NextId();
        var entity = new AiDatabase(
            tenantId,
            normalizedName,
            request.Description?.Trim(),
            request.BotId,
            schemaJson,
            id,
            request.WorkspaceId,
            request.QueryMode,
            request.ChannelScope);
        entity.SetStandaloneDriver(Atlas.Infrastructure.Services.DataSourceDriverRegistry.NormalizeDriverCode(request.DriverCode));
        var fields = BuildFieldEntities(tenantId, id, normalizedFields);
        var channelConfigs = BuildChannelConfigEntities(tenantId, id, items: null);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _databaseRepository.AddAsync(entity, cancellationToken);
            await _fieldRepository.AddRangeAsync(fields, cancellationToken);
            await _channelConfigRepository.AddRangeAsync(channelConfigs, cancellationToken);
        }, cancellationToken);

        await _provisioner.EnsureProvisionedAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(TenantId tenantId, long id, AiDatabaseUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, id, cancellationToken);
        var normalizedName = request.Name.Trim();
        if (await _databaseRepository.ExistsByNameAsync(tenantId, normalizedName, id, cancellationToken))
        {
            throw new BusinessException("数据库名称已存在。", ErrorCodes.ValidationError);
        }

        var normalizedFields = NormalizeFields(request.TableSchema, request.Fields);
        var schemaJson = SerializeSchema(normalizedFields);
        var validate = await ValidateSchemaAsync(schemaJson, cancellationToken);
        if (!validate.IsValid)
        {
            throw new BusinessException("数据库 Schema 不合法。", ErrorCodes.ValidationError);
        }

        _quotaPolicy.EnsureFieldCount(normalizedFields.Count);
        entity.Update(
            normalizedName,
            request.Description?.Trim(),
            request.BotId,
            schemaJson,
            request.QueryMode,
            request.ChannelScope,
            workspaceId: request.WorkspaceId);

        var fields = BuildFieldEntities(tenantId, id, normalizedFields);
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _databaseRepository.UpdateAsync(entity, cancellationToken);
            await _fieldRepository.DeleteByDatabaseAsync(tenantId, id, cancellationToken);
            await _fieldRepository.AddRangeAsync(fields, cancellationToken);
        }, cancellationToken);

        await EnsureStorageReadyAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, id, cancellationToken);
        var bindingCount = await _appResourceBindingRepository.CountByResourceAsync(tenantId, "database", entity.Id, cancellationToken);
        if (bindingCount > 0)
        {
            throw new BusinessException(
                $"数据库已被 {bindingCount} 个应用绑定，请先解绑后再删除。",
                ErrorCodes.ValidationError);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _fieldRepository.DeleteByDatabaseAsync(tenantId, entity.Id, cancellationToken);
            await _channelConfigRepository.DeleteByDatabaseAsync(tenantId, entity.Id, cancellationToken);
            await _recordRepository.DeleteByDatabaseAsync(tenantId, entity.Id, cancellationToken);
            await _importTaskRepository.DeleteByDatabaseAsync(tenantId, entity.Id, cancellationToken);
            await _databaseRepository.DeleteAsync(tenantId, entity.Id, cancellationToken);
        }, cancellationToken);

        if (entity.StorageMode == AiDatabaseStorageMode.Standalone)
        {
            await _provisioner.DropAsync(entity, cancellationToken);
        }
        else
        {
            await _physicalTableService.DropDatabaseTablesAsync(entity, cancellationToken);
        }
    }

    public async Task BindAsync(TenantId tenantId, long id, long botId, CancellationToken cancellationToken)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, id, cancellationToken);
        entity.BindBot(botId);
        await _databaseRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task UnbindAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, id, cancellationToken);
        entity.UnbindBot();
        await _databaseRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task<PagedResult<AiDatabaseRecordListItem>> GetRecordsAsync(
        TenantId tenantId,
        long databaseId,
        int pageIndex,
        int pageSize,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken,
        long? ownerUserId = null,
        string? channelId = null)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        await EnsureStorageReadyAsync(entity, cancellationToken);
        var policy = AiDatabaseAccessPolicy.For(entity, ownerUserId, channelId);
        var (items, total) = await _physicalTableService.GetPagedRowsAsync(entity, environment, policy, pageIndex, pageSize, cancellationToken);

        return new PagedResult<AiDatabaseRecordListItem>(
            items.Select(row => MapRecord(entity.Id, environment, row)).ToList(),
            total,
            pageIndex,
            pageSize);
    }

    public async Task<long> CreateRecordAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordCreateRequest request,
        CancellationToken cancellationToken,
        long? ownerUserId = null,
        long? creatorUserId = null,
        string? channelId = null)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        await EnsureStorageReadyAsync(entity, cancellationToken);
        await EnsureWriteAllowedAsync(entity, request.Environment, channelId, cancellationToken);
        EnsureJsonObject(request.DataJson);
        var coercedJson = AiDatabaseValueCoercer.Coerce(entity.TableSchema, request.DataJson);
        await _quotaPolicy.EnsureCanAddRowAsync(tenantId, databaseId, incoming: 1, cancellationToken);

        var row = new AiDatabasePhysicalRow(
            _idGeneratorAccessor.NextId(),
            coercedJson,
            ownerUserId,
            creatorUserId,
            channelId,
            DateTime.UtcNow,
            DateTime.UtcNow);
        await _physicalTableService.InsertRowAsync(entity, request.Environment, row, cancellationToken);
        await UpdateDatabaseCountsAsync(entity, cancellationToken);
        return row.Id;
    }

    public async Task<AiDatabaseRecordBulkCreateResult> CreateRecordsBulkAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordBulkCreateRequest request,
        CancellationToken cancellationToken,
        long? ownerUserId = null,
        long? creatorUserId = null,
        string? channelId = null,
        bool enforceSyncBulkRowLimit = true)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        await EnsureStorageReadyAsync(entity, cancellationToken);
        await EnsureWriteAllowedAsync(entity, request.Environment, channelId, cancellationToken);

        var rows = request.Rows ?? [];
        if (rows.Count == 0)
        {
            return new AiDatabaseRecordBulkCreateResult(0, 0, 0, Array.Empty<AiDatabaseRecordBulkRowResult>());
        }

        if (enforceSyncBulkRowLimit)
        {
            _quotaPolicy.EnsureBulkInsertSize(rows.Count);
        }

        await _quotaPolicy.EnsureCanAddRowAsync(tenantId, databaseId, incoming: rows.Count, cancellationToken);

        var rowResults = new List<AiDatabaseRecordBulkRowResult>(rows.Count);
        var validRows = new List<AiDatabasePhysicalRow>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                EnsureJsonObject(rows[i]);
                var coercedJson = AiDatabaseValueCoercer.Coerce(entity.TableSchema, rows[i]);
                var row = new AiDatabasePhysicalRow(
                    _idGeneratorAccessor.NextId(),
                    coercedJson,
                    ownerUserId,
                    creatorUserId,
                    channelId,
                    DateTime.UtcNow,
                    DateTime.UtcNow);
                validRows.Add(row);
                rowResults.Add(new AiDatabaseRecordBulkRowResult(i, true, row.Id.ToString(), null));
            }
            catch (BusinessException bex)
            {
                rowResults.Add(new AiDatabaseRecordBulkRowResult(i, false, null, bex.Message));
            }
            catch (Exception ex)
            {
                rowResults.Add(new AiDatabaseRecordBulkRowResult(
                    i,
                    false,
                    null,
                    AiDatabasePublicErrors.ForRow(ex, _logger, i, databaseId)));
            }
        }

        if (validRows.Count > 0)
        {
            await _physicalTableService.InsertRowsAsync(entity, request.Environment, validRows, cancellationToken);
            await UpdateDatabaseCountsAsync(entity, cancellationToken);
        }

        return new AiDatabaseRecordBulkCreateResult(rows.Count, validRows.Count, rows.Count - validRows.Count, rowResults);
    }

    public async Task<AiDatabaseBulkJobAccepted> SubmitBulkInsertJobAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordBulkCreateRequest request,
        CancellationToken cancellationToken,
        long? ownerUserId = null,
        long? creatorUserId = null,
        string? channelId = null)
    {
        await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        var rows = request.Rows ?? [];
        if (rows.Count == 0)
        {
            throw new BusinessException("批量任务行数为空。", ErrorCodes.ValidationError);
        }

        var maxAsync = Math.Max(_quotaPolicy.Options.MaxBulkInsertRows * 50, 50_000);
        if (rows.Count > maxAsync)
        {
            throw new BusinessException($"批量异步任务行数超过上限（{maxAsync}）。", ErrorCodes.ValidationError);
        }

        var payloadJson = JsonSerializer.Serialize(rows);
        var task = new AiDatabaseImportTask(
            tenantId,
            databaseId,
            fileId: 0L,
            id: _idGeneratorAccessor.NextId(),
            source: AiDatabaseImportSource.Inline,
            payloadJson: payloadJson,
            ownerUserId: ownerUserId,
            creatorUserId: creatorUserId,
            channelId: channelId,
            environment: request.Environment);
        await _importTaskRepository.AddAsync(task, cancellationToken);

        _backgroundWorkQueue.Enqueue((sp, ct) =>
            ProcessInlineBulkAsync(sp, tenantId, databaseId, task.Id, ownerUserId, creatorUserId, channelId, request.Environment, ct));
        return new AiDatabaseBulkJobAccepted(task.Id, rows.Count);
    }

    public async Task UpdateRecordAsync(
        TenantId tenantId,
        long databaseId,
        long recordId,
        AiDatabaseRecordUpdateRequest request,
        CancellationToken cancellationToken,
        long? ownerUserId = null,
        string? channelId = null)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        await EnsureStorageReadyAsync(entity, cancellationToken);
        await EnsureWriteAllowedAsync(entity, request.Environment, channelId, cancellationToken);
        var policy = AiDatabaseAccessPolicy.For(entity, ownerUserId, channelId);
        var existing = await _physicalTableService.FindRowAsync(entity, request.Environment, recordId, cancellationToken)
            ?? throw new BusinessException("数据库记录不存在。", ErrorCodes.NotFound);
        if (!policy.IsRecordVisible(existing.OwnerUserId, existing.ChannelId))
        {
            throw new BusinessException("当前上下文无权修改该记录。", ErrorCodes.Forbidden);
        }

        EnsureJsonObject(request.DataJson);
        var coercedJson = AiDatabaseValueCoercer.Coerce(entity.TableSchema, request.DataJson);
        await _physicalTableService.UpdateRowAsync(
            entity,
            request.Environment,
            existing with
            {
                DataJson = coercedJson,
                UpdatedAt = DateTime.UtcNow
            },
            cancellationToken);
    }

    public async Task DeleteRecordAsync(
        TenantId tenantId,
        long databaseId,
        long recordId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken,
        long? ownerUserId = null,
        string? channelId = null)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        await EnsureStorageReadyAsync(entity, cancellationToken);
        var existing = await _physicalTableService.FindRowAsync(entity, environment, recordId, cancellationToken)
            ?? throw new BusinessException("数据库记录不存在。", ErrorCodes.NotFound);
        var policy = AiDatabaseAccessPolicy.For(entity, ownerUserId, channelId);
        if (!policy.IsRecordVisible(existing.OwnerUserId, existing.ChannelId))
        {
            throw new BusinessException("当前上下文无权删除该记录。", ErrorCodes.Forbidden);
        }

        await _physicalTableService.DeleteRowAsync(entity, environment, recordId, cancellationToken);
        await UpdateDatabaseCountsAsync(entity, cancellationToken);
    }

    public async Task<string> GetSchemaAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, id, cancellationToken);
        return entity.TableSchema;
    }

    public async Task UpdateModesAsync(
        TenantId tenantId,
        long id,
        AiDatabaseModeUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, id, cancellationToken);
        entity.SetQueryMode(request.QueryMode, request.ChannelScope);
        await _databaseRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyList<AiDatabaseChannelConfigItem>> GetChannelConfigsAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, id, cancellationToken);
        return (await GetOrBootstrapChannelConfigsAsync(entity, cancellationToken))
            .Select(MapChannelConfig)
            .ToList();
    }

    public async Task UpdateChannelConfigsAsync(
        TenantId tenantId,
        long id,
        AiDatabaseChannelConfigsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(tenantId, id, cancellationToken);
        var entities = BuildChannelConfigEntities(tenantId, id, request.Items);
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _channelConfigRepository.DeleteByDatabaseAsync(tenantId, id, cancellationToken);
            await _channelConfigRepository.AddRangeAsync(entities, cancellationToken);
        }, cancellationToken);
    }

    public Task<AiDatabaseSchemaValidateResult> ValidateSchemaAsync(string schemaJson, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<string>();
        var columns = TryParseSchemaColumns(schemaJson, errors);
        if (columns.Count == 0)
        {
            errors.Add("Schema 至少包含 1 列。");
        }

        return Task.FromResult(new AiDatabaseSchemaValidateResult(errors.Count == 0, errors));
    }

    public async Task<long> SubmitImportAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseImportRequest request,
        CancellationToken cancellationToken,
        long? ownerUserId = null,
        long? creatorUserId = null,
        string? channelId = null)
    {
        await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        var fileInfo = await _fileStorageService.GetInfoAsync(tenantId, request.FileId, cancellationToken);
        if (fileInfo is null)
        {
            throw new BusinessException("导入文件不存在。", ErrorCodes.NotFound);
        }

        var importTask = new AiDatabaseImportTask(
            tenantId,
            databaseId,
            request.FileId,
            _idGeneratorAccessor.NextId(),
            AiDatabaseImportSource.File,
            payloadJson: null,
            ownerUserId: ownerUserId,
            creatorUserId: creatorUserId,
            channelId: channelId,
            environment: request.Environment);
        await _importTaskRepository.AddAsync(importTask, cancellationToken);

        _backgroundWorkQueue.Enqueue((sp, ct) =>
            ProcessImportAsync(sp, tenantId, databaseId, importTask.Id, request.FileId, request.Environment, ct));
        return importTask.Id;
    }

    public async Task<AiDatabaseImportProgress?> GetImportProgressAsync(
        TenantId tenantId,
        long databaseId,
        CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        var task = await _importTaskRepository.GetLatestAsync(tenantId, databaseId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        return new AiDatabaseImportProgress(
            task.Id,
            task.DatabaseId,
            task.Status,
            task.TotalRows,
            task.SucceededRows,
            task.FailedRows,
            task.ErrorMessage,
            task.CreatedAt,
            task.UpdatedAt,
            task.Source,
            task.Environment);
    }

    public async Task<AiDatabaseTemplate> GetTemplateAsync(
        TenantId tenantId,
        long databaseId,
        CancellationToken cancellationToken)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        var fields = await GetOrBootstrapFieldsAsync(entity, cancellationToken);
        var csv = string.Join(",", fields.Where(x => !x.IsSystemField).Select(x => x.Name)) + Environment.NewLine;
        return new AiDatabaseTemplate($"{entity.Name}-template.csv", "text/csv", Encoding.UTF8.GetBytes(csv));
    }

    private async Task<AiDatabase> EnsureDatabaseExistsAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _databaseRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("数据库不存在。", ErrorCodes.NotFound);
    }

    private async Task EnsureStorageReadyAsync(AiDatabase database, CancellationToken cancellationToken)
    {
        if (database.StorageMode == AiDatabaseStorageMode.Standalone)
        {
            await _provisioner.EnsureProvisionedAsync(database, cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(database.DraftTableName) || string.IsNullOrWhiteSpace(database.OnlineTableName))
        {
            var names = _physicalTableService.BuildTableNames(database.TenantId, database.Id);
            database.SetPhysicalTables(names.DraftTableName, names.OnlineTableName);
            await _databaseRepository.UpdateAsync(database, cancellationToken);
        }

        var legacyRows = await _recordRepository.GetPagedByDatabaseAsync(
            database.TenantId,
            database.Id,
            1,
            5000,
            cancellationToken);
        await _physicalTableService.EnsureDatabaseTablesAsync(database, legacyRows.Items, cancellationToken);
    }

    private async Task UpdateDatabaseCountsAsync(AiDatabase database, CancellationToken cancellationToken)
    {
        var draftCount = await _physicalTableService.CountRowsAsync(database, AiDatabaseRecordEnvironment.Draft, cancellationToken);
        var onlineCount = await _physicalTableService.CountRowsAsync(database, AiDatabaseRecordEnvironment.Online, cancellationToken);
        database.SetRecordCount(draftCount + onlineCount);
        await _databaseRepository.UpdateAsync(database, cancellationToken);
    }

    private async Task<IReadOnlyList<AiDatabaseField>> GetOrBootstrapFieldsAsync(AiDatabase database, CancellationToken cancellationToken)
    {
        var fields = await _fieldRepository.ListByDatabaseAsync(database.TenantId, database.Id, cancellationToken);
        if (fields.Count > 0)
        {
            return fields;
        }

        var normalizedFields = NormalizeFields(database.TableSchema, fields: null);
        fields = BuildFieldEntities(database.TenantId, database.Id, normalizedFields);
        await _fieldRepository.AddRangeAsync(fields, cancellationToken);
        return fields;
    }

    private async Task<IReadOnlyList<AiDatabaseChannelConfig>> GetOrBootstrapChannelConfigsAsync(AiDatabase database, CancellationToken cancellationToken)
    {
        var items = await _channelConfigRepository.ListByDatabaseAsync(database.TenantId, database.Id, cancellationToken);
        if (items.Count > 0)
        {
            return items;
        }

        items = BuildChannelConfigEntities(database.TenantId, database.Id, items: null);
        await _channelConfigRepository.AddRangeAsync(items, cancellationToken);
        return items;
    }

    private async Task EnsureWriteAllowedAsync(
        AiDatabase database,
        AiDatabaseRecordEnvironment environment,
        string? channelId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var channelConfigs = await GetOrBootstrapChannelConfigsAsync(database, cancellationToken);
        if (string.IsNullOrWhiteSpace(channelId))
        {
            return;
        }

        var matched = channelConfigs.FirstOrDefault(x => string.Equals(x.ChannelKey, channelId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (matched is null)
        {
            return;
        }

        var allowed = environment == AiDatabaseRecordEnvironment.Online ? matched.AllowOnline : matched.AllowDraft;
        if (!allowed)
        {
            throw new BusinessException("当前渠道未启用对应数据域的读写权限。", ErrorCodes.Forbidden);
        }
    }

    /// <summary>D5：处理内联 JSON 批量插入异步任务（行级 owner/channel 透传）。</summary>
    private async Task ProcessInlineBulkAsync(
        IServiceProvider serviceProvider,
        TenantId tenantId,
        long databaseId,
        long taskId,
        long? ownerUserId,
        long? creatorUserId,
        string? channelId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        var importTaskRepository = serviceProvider.GetRequiredService<AiDatabaseImportTaskRepository>();
        var task = await importTaskRepository.FindByIdAsync(tenantId, taskId, cancellationToken);
        if (task is null)
        {
            _logger.LogWarning("数据库批量任务不存在。taskId={TaskId}", taskId);
            return;
        }

        try
        {
            task.MarkRunning();
            await importTaskRepository.UpdateAsync(task, cancellationToken);
            if (string.IsNullOrWhiteSpace(task.PayloadJson))
            {
                task.MarkFailed(AiDatabasePublicErrors.BulkPayloadInvalid);
                await importTaskRepository.UpdateAsync(task, cancellationToken);
                return;
            }

            var rows = JsonSerializer.Deserialize<List<string>>(task.PayloadJson) ?? [];
            if (rows.Count == 0)
            {
                task.MarkFailed(AiDatabasePublicErrors.InlineJobEmptyPayload);
                await importTaskRepository.UpdateAsync(task, cancellationToken);
                return;
            }

            var aiDatabaseService = serviceProvider.GetRequiredService<IAiDatabaseService>();
            var result = await aiDatabaseService.CreateRecordsBulkAsync(
                tenantId,
                databaseId,
                new AiDatabaseRecordBulkCreateRequest(rows, environment),
                cancellationToken,
                ownerUserId,
                creatorUserId,
                channelId,
                enforceSyncBulkRowLimit: false);

            task.MarkCompleted(result.Total, result.Succeeded, result.Failed);
            await importTaskRepository.UpdateAsync(task, cancellationToken);
        }
        catch (Exception ex)
        {
            task.MarkFailed(AiDatabasePublicErrors.ForJob(ex, _logger, databaseId, taskId));
            await importTaskRepository.UpdateAsync(task, cancellationToken);
        }
    }

    private async Task ProcessImportAsync(
        IServiceProvider serviceProvider,
        TenantId tenantId,
        long databaseId,
        long taskId,
        long fileId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken)
    {
        var databaseRepository = serviceProvider.GetRequiredService<AiDatabaseRepository>();
        var importTaskRepository = serviceProvider.GetRequiredService<AiDatabaseImportTaskRepository>();
        var fileStorageService = serviceProvider.GetRequiredService<IFileStorageService>();
        var aiDatabaseService = serviceProvider.GetRequiredService<IAiDatabaseService>();

        var task = await importTaskRepository.FindByIdAsync(tenantId, taskId, cancellationToken);
        if (task is null)
        {
            _logger.LogWarning("数据库导入任务不存在。taskId={TaskId}", taskId);
            return;
        }

        var database = await databaseRepository.FindByIdAsync(tenantId, databaseId, cancellationToken);
        if (database is null)
        {
            task.MarkFailed(AiDatabasePublicErrors.ImportTargetMissing);
            await importTaskRepository.UpdateAsync(task, cancellationToken);
            return;
        }

        try
        {
            task.MarkRunning();
            await importTaskRepository.UpdateAsync(task, cancellationToken);
            var file = await fileStorageService.DownloadAsync(tenantId, fileId, cancellationToken);
            var rows = await ReadCsvRowsAsync(file.Stream, cancellationToken);
            if (rows.Count == 0)
            {
                task.MarkCompleted(0, 0, 0);
                await importTaskRepository.UpdateAsync(task, cancellationToken);
                return;
            }

            var payloads = rows.Select(row => JsonSerializer.Serialize(row)).ToArray();
            var result = await aiDatabaseService.CreateRecordsBulkAsync(
                tenantId,
                databaseId,
                new AiDatabaseRecordBulkCreateRequest(payloads, environment),
                cancellationToken,
                task.OwnerUserId,
                task.CreatorUserId,
                task.ChannelId,
                enforceSyncBulkRowLimit: false);

            task.MarkCompleted(result.Total, result.Succeeded, result.Failed);
            await importTaskRepository.UpdateAsync(task, cancellationToken);
        }
        catch (Exception ex)
        {
            task.MarkFailed(AiDatabasePublicErrors.ForImport(ex, _logger, databaseId, taskId));
            await importTaskRepository.UpdateAsync(task, cancellationToken);
        }
    }

    private List<AiDatabaseFieldItem> NormalizeFields(string? schemaJson, IReadOnlyList<AiDatabaseFieldItem>? fields)
    {
        var result = new List<AiDatabaseFieldItem>();
        result.AddRange(CreateSystemFields());

        if (fields is { Count: > 0 })
        {
            var userFields = fields
                .Where(x => !x.IsSystemField)
                .Select((field, index) => field with
                {
                    Name = field.Name.Trim(),
                    Type = NormalizeFieldType(field.Type),
                    SortOrder = index
                })
                .ToList();
            result.AddRange(userFields);
            return DeduplicateFields(result);
        }

        var parsed = ParseFieldItemsFromSchema(schemaJson);
        result.AddRange(parsed.Select((field, index) => field with { SortOrder = index }));
        return DeduplicateFields(result);
    }

    private static List<AiDatabaseFieldItem> CreateSystemFields()
    {
        return
        [
            new(null, "id", "数据主键", "integer", true, true, true, 0),
            new(null, "sys_platform", "数据所属渠道", "string", false, false, true, 1),
            new(null, "uuid", "创建用户标识", "string", false, true, true, 2),
            new(null, "bstudio_create_time", "创建时间", "date", true, true, true, 3)
        ];
    }

    private static List<AiDatabaseFieldItem> DeduplicateFields(List<AiDatabaseFieldItem> fields)
    {
        var result = new List<AiDatabaseFieldItem>(fields.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Name) || !seen.Add(field.Name.Trim()))
            {
                continue;
            }

            result.Add(field with { Name = field.Name.Trim() });
        }

        return result;
    }

    private static List<AiDatabaseFieldItem> ParseFieldItemsFromSchema(string? schemaJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson))
        {
            return [new(null, "name", "名称", "string", false, false, false, 0)];
        }

        try
        {
            var node = JsonNode.Parse(schemaJson);
            if (node is not JsonArray array)
            {
                return [new(null, "name", "名称", "string", false, false, false, 0)];
            }

            var list = new List<AiDatabaseFieldItem>(array.Count);
            foreach (var item in array)
            {
                if (item is not JsonObject obj)
                {
                    continue;
                }

                var name = obj["name"]?.GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                list.Add(new AiDatabaseFieldItem(
                    null,
                    name,
                    obj["description"]?.GetValue<string>() ?? obj["desc"]?.GetValue<string>(),
                    NormalizeFieldType(obj["type"]?.GetValue<string>()),
                    obj["required"]?.GetValue<bool>() ?? obj["must_required"]?.GetValue<bool>() ?? false,
                    obj["indexed"]?.GetValue<bool>() ?? obj["is_primary_key"]?.GetValue<bool>() ?? false,
                    false,
                    list.Count));
            }

            return list.Count > 0 ? list : [new(null, "name", "名称", "string", false, false, false, 0)];
        }
        catch (JsonException)
        {
            return [new(null, "name", "名称", "string", false, false, false, 0)];
        }
    }

    private static string SerializeSchema(IReadOnlyList<AiDatabaseFieldItem> fields)
    {
        var payload = fields
            .Where(x => !x.IsSystemField)
            .Select(x => new Dictionary<string, object?>
            {
                ["name"] = x.Name,
                ["description"] = x.Description,
                ["type"] = NormalizeFieldType(x.Type),
                ["required"] = x.Required,
                ["indexed"] = x.Indexed
            })
            .ToList();
        return JsonSerializer.Serialize(payload);
    }

    private List<AiDatabaseField> BuildFieldEntities(TenantId tenantId, long databaseId, IReadOnlyList<AiDatabaseFieldItem> fields)
    {
        return fields
            .Select((field, index) => new AiDatabaseField(
                tenantId,
                databaseId,
                field.Name,
                field.Description,
                NormalizeFieldType(field.Type),
                field.Required,
                field.IsSystemField,
                field.Indexed,
                index,
                _idGeneratorAccessor.NextId()))
            .ToList();
    }

    private List<AiDatabaseChannelConfig> BuildChannelConfigEntities(
        TenantId tenantId,
        long databaseId,
        IReadOnlyList<AiDatabaseChannelConfigItem>? items)
    {
        var source = items is { Count: > 0 }
            ? items.ToList()
            : AiDatabaseChannelCatalog.All
                .Select((item, index) => new AiDatabaseChannelConfigItem(
                    item.ChannelKey,
                    item.DisplayName,
                    item.AllowDraft,
                    item.AllowOnline,
                    item.PublishChannelType,
                    item.CredentialKind,
                    index))
                .ToList();

        return source
            .Select((item, index) => new AiDatabaseChannelConfig(
                tenantId,
                databaseId,
                item.ChannelKey,
                item.DisplayName,
                item.AllowDraft,
                item.AllowOnline,
                item.PublishChannelType,
                item.CredentialKind,
                index,
                _idGeneratorAccessor.NextId()))
            .ToList();
    }

    private static void EnsureJsonObject(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new BusinessException("记录数据必须是 JSON 对象。", ErrorCodes.ValidationError);
            }
        }
        catch (JsonException)
        {
            throw new BusinessException("记录数据不是合法 JSON。", ErrorCodes.ValidationError);
        }
    }

    private static IReadOnlyList<string> TryParseSchemaColumns(string schemaJson, List<string> errors)
    {
        try
        {
            var node = JsonNode.Parse(schemaJson);
            if (node is not JsonArray array)
            {
                errors.Add("Schema 必须是 JSON 数组。");
                return [];
            }

            var columns = new List<string>();
            foreach (var item in array)
            {
                if (item is not JsonObject obj)
                {
                    errors.Add("Schema 项必须为对象。");
                    continue;
                }

                var name = obj["name"]?.GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add("Schema 列缺少 name。");
                    continue;
                }

                if (columns.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"Schema 列名重复：{name}");
                    continue;
                }

                columns.Add(name);
            }

            return columns;
        }
        catch (JsonException)
        {
            errors.Add("Schema 不是合法 JSON。");
            return [];
        }
    }

    private static async Task<List<Dictionary<string, string?>>> ReadCsvRowsAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        var content = await reader.ReadToEndAsync(cancellationToken);
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length <= 1)
        {
            return [];
        }

        var headers = lines[0]
            .Split(',', StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
        if (headers.Length == 0)
        {
            return [];
        }

        var rows = new List<Dictionary<string, string?>>(Math.Max(0, lines.Length - 1));
        for (var i = 1; i < lines.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = lines[i].Split(',', StringSplitOptions.None);
            var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < headers.Length; j++)
            {
                row[headers[j]] = j < values.Length ? values[j]?.Trim() : null;
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string NormalizeFieldType(string? type)
        => string.IsNullOrWhiteSpace(type) ? "string" : type.Trim().ToLowerInvariant();

    private static AiDatabaseListItem MapListItem(AiDatabase entity, int draftRecordCount, int onlineRecordCount)
        => new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.BotId,
            draftRecordCount + onlineRecordCount,
            draftRecordCount,
            onlineRecordCount,
            entity.QueryMode,
            entity.ChannelScope,
            entity.StorageMode,
            entity.DriverCode,
            entity.ProvisionState,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static AiDatabaseDetail MapDetail(
        AiDatabase entity,
        IReadOnlyList<AiDatabaseField> fields,
        IReadOnlyList<AiDatabaseChannelConfig> channelConfigs,
        int draftRecordCount,
        int onlineRecordCount)
        => new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.BotId,
            entity.TableSchema,
            draftRecordCount + onlineRecordCount,
            draftRecordCount,
            onlineRecordCount,
            entity.QueryMode,
            entity.ChannelScope,
            entity.StorageMode,
            entity.DriverCode,
            entity.ProvisionState,
            entity.ProvisionError,
            entity.WorkspaceId,
            fields.Select(MapField).ToList(),
            channelConfigs.Select(MapChannelConfig).ToList(),
            entity.CreatedAt,
            entity.UpdatedAt);

    private static AiDatabaseFieldItem MapField(AiDatabaseField entity)
        => new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.FieldType,
            entity.Required,
            entity.Indexed,
            entity.IsSystemField,
            entity.SortOrder);

    private static AiDatabaseChannelConfigItem MapChannelConfig(AiDatabaseChannelConfig entity)
        => new(
            entity.ChannelKey,
            entity.DisplayName,
            entity.AllowDraft,
            entity.AllowOnline,
            entity.PublishChannelType,
            entity.CredentialKind,
            entity.SortOrder);

    private static AiDatabaseRecordListItem MapRecord(long databaseId, AiDatabaseRecordEnvironment environment, AiDatabasePhysicalRow row)
        => new(
            row.Id,
            databaseId,
            row.DataJson,
            environment,
            row.OwnerUserId,
            row.CreatorUserId,
            row.ChannelId,
            row.CreatedAt,
            row.UpdatedAt);
}
