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
    private readonly AiDatabaseRecordRepository _recordRepository;
    private readonly AiDatabaseImportTaskRepository _importTaskRepository;
    private readonly AiAppResourceBindingRepository _appResourceBindingRepository;
    private readonly AiDatabaseQuotaPolicy _quotaPolicy;
    private readonly IFileStorageService _fileStorageService;
    private readonly IBackgroundWorkQueue _backgroundWorkQueue;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AiDatabaseService> _logger;

    public AiDatabaseService(
        AiDatabaseRepository databaseRepository,
        AiDatabaseRecordRepository recordRepository,
        AiDatabaseImportTaskRepository importTaskRepository,
        AiAppResourceBindingRepository appResourceBindingRepository,
        AiDatabaseQuotaPolicy quotaPolicy,
        IFileStorageService fileStorageService,
        IBackgroundWorkQueue backgroundWorkQueue,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork,
        ILogger<AiDatabaseService> logger)
    {
        _databaseRepository = databaseRepository;
        _recordRepository = recordRepository;
        _importTaskRepository = importTaskRepository;
        _appResourceBindingRepository = appResourceBindingRepository;
        _quotaPolicy = quotaPolicy;
        _fileStorageService = fileStorageService;
        _backgroundWorkQueue = backgroundWorkQueue;
        _idGeneratorAccessor = idGeneratorAccessor;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PagedResult<AiDatabaseListItem>> GetPagedAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _databaseRepository.GetPagedAsync(
            tenantId,
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);
        return new PagedResult<AiDatabaseListItem>(
            items.Select(MapListItem).ToList(),
            total,
            pageIndex,
            pageSize);
    }

    public async Task<AiDatabaseDetail?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _databaseRepository.FindByIdAsync(tenantId, id, cancellationToken);
        return entity is null ? null : MapDetail(entity);
    }

    public async Task<long> CreateAsync(TenantId tenantId, AiDatabaseCreateRequest request, CancellationToken cancellationToken)
    {
        var normalizedName = request.Name.Trim();
        if (await _databaseRepository.ExistsByNameAsync(tenantId, normalizedName, excludeId: null, cancellationToken))
        {
            throw new BusinessException("数据库名称已存在。", ErrorCodes.ValidationError);
        }

        var validate = await ValidateSchemaAsync(request.TableSchema, cancellationToken);
        if (!validate.IsValid)
        {
            throw new BusinessException("数据库 Schema 不合法。", ErrorCodes.ValidationError);
        }

        await _quotaPolicy.EnsureCanCreateDatabaseAsync(tenantId, cancellationToken);
        _quotaPolicy.EnsureFieldCount(AiDatabaseValueCoercer.ParseColumns(request.TableSchema).Count);

        var entity = new AiDatabase(
            tenantId,
            normalizedName,
            request.Description?.Trim(),
            request.BotId,
            request.TableSchema,
            _idGeneratorAccessor.NextId(),
            request.WorkspaceId);
        await _databaseRepository.AddAsync(entity, cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(TenantId tenantId, long id, AiDatabaseUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _databaseRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("数据库不存在。", ErrorCodes.NotFound);
        var normalizedName = request.Name.Trim();
        if (await _databaseRepository.ExistsByNameAsync(tenantId, normalizedName, id, cancellationToken))
        {
            throw new BusinessException("数据库名称已存在。", ErrorCodes.ValidationError);
        }

        var validate = await ValidateSchemaAsync(request.TableSchema, cancellationToken);
        if (!validate.IsValid)
        {
            throw new BusinessException("数据库 Schema 不合法。", ErrorCodes.ValidationError);
        }

        _quotaPolicy.EnsureFieldCount(AiDatabaseValueCoercer.ParseColumns(request.TableSchema).Count);

        entity.Update(normalizedName, request.Description?.Trim(), request.BotId, request.TableSchema, workspaceId: request.WorkspaceId);
        await _databaseRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _databaseRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("数据库不存在。", ErrorCodes.NotFound);

        // X1：阻止删除已被任意 App 绑定的数据库。
        var bindingCount = await _appResourceBindingRepository.CountByResourceAsync(
            tenantId, "database", entity.Id, cancellationToken);
        if (bindingCount > 0)
        {
            throw new BusinessException(
                $"数据库已被 {bindingCount} 个应用绑定，请先解绑后再删除。",
                ErrorCodes.ValidationError);
        }

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _recordRepository.DeleteByDatabaseAsync(tenantId, entity.Id, cancellationToken);
            await _importTaskRepository.DeleteByDatabaseAsync(tenantId, entity.Id, cancellationToken);
            await _databaseRepository.DeleteAsync(tenantId, entity.Id, cancellationToken);
        }, cancellationToken);
    }

    public async Task BindAsync(TenantId tenantId, long id, long botId, CancellationToken cancellationToken)
    {
        var entity = await _databaseRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("数据库不存在。", ErrorCodes.NotFound);
        entity.BindBot(botId);
        await _databaseRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task UnbindAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _databaseRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("数据库不存在。", ErrorCodes.NotFound);
        entity.UnbindBot();
        await _databaseRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task<PagedResult<AiDatabaseRecordListItem>> GetRecordsAsync(
        TenantId tenantId,
        long databaseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        var (items, total) = await _recordRepository.GetPagedByDatabaseAsync(
            tenantId,
            databaseId,
            pageIndex,
            pageSize,
            cancellationToken);
        return new PagedResult<AiDatabaseRecordListItem>(
            items.Select(MapRecord).ToList(),
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
        EnsureJsonObject(request.DataJson);
        var coercedJson = AiDatabaseValueCoercer.Coerce(entity.TableSchema, request.DataJson);
        await _quotaPolicy.EnsureCanAddRowAsync(tenantId, databaseId, incoming: 1, cancellationToken);

        var record = new AiDatabaseRecord(
            tenantId,
            databaseId,
            coercedJson,
            _idGeneratorAccessor.NextId(),
            ownerUserId,
            creatorUserId,
            channelId);
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _recordRepository.AddAsync(record, cancellationToken);
            var count = await _recordRepository.CountByDatabaseAsync(tenantId, databaseId, cancellationToken);
            entity.SetRecordCount(count);
            await _databaseRepository.UpdateAsync(entity, cancellationToken);
        }, cancellationToken);
        return record.Id;
    }

    public async Task<AiDatabaseRecordBulkCreateResult> CreateRecordsBulkAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordBulkCreateRequest request,
        CancellationToken cancellationToken,
        long? ownerUserId = null,
        long? creatorUserId = null,
        string? channelId = null)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        var rows = request.Rows ?? [];
        if (rows.Count == 0)
        {
            return new AiDatabaseRecordBulkCreateResult(0, 0, 0, Array.Empty<AiDatabaseRecordBulkRowResult>());
        }

        _quotaPolicy.EnsureBulkInsertSize(rows.Count);
        await _quotaPolicy.EnsureCanAddRowAsync(tenantId, databaseId, incoming: rows.Count, cancellationToken);

        var rowResults = new List<AiDatabaseRecordBulkRowResult>(rows.Count);
        var validRecords = new List<AiDatabaseRecord>(rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                EnsureJsonObject(rows[i]);
                var coercedJson = AiDatabaseValueCoercer.Coerce(entity.TableSchema, rows[i]);
                var record = new AiDatabaseRecord(
                    tenantId,
                    databaseId,
                    coercedJson,
                    _idGeneratorAccessor.NextId(),
                    ownerUserId,
                    creatorUserId,
                    channelId);
                validRecords.Add(record);
                rowResults.Add(new AiDatabaseRecordBulkRowResult(i, true, record.Id.ToString(), null));
            }
            catch (BusinessException bex)
            {
                rowResults.Add(new AiDatabaseRecordBulkRowResult(i, false, null, bex.Message));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AiDatabase 批量插入第 {Index} 行处理异常 db={DatabaseId}", i, databaseId);
                rowResults.Add(new AiDatabaseRecordBulkRowResult(i, false, null, ex.Message));
            }
        }

        if (validRecords.Count > 0)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _recordRepository.AddRangeAsync(validRecords, cancellationToken);
                var count = await _recordRepository.CountByDatabaseAsync(tenantId, databaseId, cancellationToken);
                entity.SetRecordCount(count);
                await _databaseRepository.UpdateAsync(entity, cancellationToken);
            }, cancellationToken);
        }

        var succeeded = validRecords.Count;
        var failed = rows.Count - succeeded;
        return new AiDatabaseRecordBulkCreateResult(rows.Count, succeeded, failed, rowResults);
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

        // 异步任务允许超过同步上限，仅按 (软) 行数上限校验整体规模。
        var maxAsync = Math.Max(_quotaPolicy.Options.MaxBulkInsertRows * 50, 50_000);
        if (rows.Count > maxAsync)
        {
            throw new BusinessException(
                $"批量异步任务行数超过上限（{maxAsync}）。",
                ErrorCodes.ValidationError);
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
            channelId: channelId);
        await _importTaskRepository.AddAsync(task, cancellationToken);

        _backgroundWorkQueue.Enqueue((sp, ct) =>
            ProcessInlineBulkAsync(sp, tenantId, databaseId, task.Id, ownerUserId, creatorUserId, channelId, ct));
        return new AiDatabaseBulkJobAccepted(task.Id, rows.Count);
    }

    public async Task UpdateRecordAsync(
        TenantId tenantId,
        long databaseId,
        long recordId,
        AiDatabaseRecordUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        EnsureJsonObject(request.DataJson);
        var coercedJson = AiDatabaseValueCoercer.Coerce(entity.TableSchema, request.DataJson);

        var record = await _recordRepository.FindByDatabaseAndIdAsync(tenantId, databaseId, recordId, cancellationToken)
            ?? throw new BusinessException("数据库记录不存在。", ErrorCodes.NotFound);
        record.UpdateData(coercedJson);
        await _recordRepository.UpdateAsync(record, cancellationToken);
    }

    public async Task DeleteRecordAsync(
        TenantId tenantId,
        long databaseId,
        long recordId,
        CancellationToken cancellationToken)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        var record = await _recordRepository.FindByDatabaseAndIdAsync(tenantId, databaseId, recordId, cancellationToken)
            ?? throw new BusinessException("数据库记录不存在。", ErrorCodes.NotFound);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _recordRepository.DeleteAsync(tenantId, record.Id, cancellationToken);
            var count = await _recordRepository.CountByDatabaseAsync(tenantId, databaseId, cancellationToken);
            entity.SetRecordCount(count);
            await _databaseRepository.UpdateAsync(entity, cancellationToken);
        }, cancellationToken);
    }

    public async Task<string> GetSchemaAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, id, cancellationToken);
        return entity.TableSchema;
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
            channelId: channelId);
        await _importTaskRepository.AddAsync(importTask, cancellationToken);

        _backgroundWorkQueue.Enqueue((sp, ct) => ProcessImportAsync(sp, tenantId, databaseId, importTask.Id, request.FileId, ct));
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
            task.Source);
    }

    public async Task<AiDatabaseTemplate> GetTemplateAsync(
        TenantId tenantId,
        long databaseId,
        CancellationToken cancellationToken)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        var errors = new List<string>();
        var columns = TryParseSchemaColumns(entity.TableSchema, errors);
        if (columns.Count == 0)
        {
            throw new BusinessException("数据库 Schema 不合法，无法生成模板。", ErrorCodes.ValidationError);
        }

        var csv = string.Join(",", columns) + Environment.NewLine;
        return new AiDatabaseTemplate(
            $"{entity.Name}-template.csv",
            "text/csv",
            Encoding.UTF8.GetBytes(csv));
    }

    private async Task<AiDatabase> EnsureDatabaseExistsAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        return await _databaseRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("数据库不存在。", ErrorCodes.NotFound);
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
        CancellationToken cancellationToken)
    {
        var databaseRepository = serviceProvider.GetRequiredService<AiDatabaseRepository>();
        var recordRepository = serviceProvider.GetRequiredService<AiDatabaseRecordRepository>();
        var importTaskRepository = serviceProvider.GetRequiredService<AiDatabaseImportTaskRepository>();
        var idGeneratorAccessor = serviceProvider.GetRequiredService<IIdGeneratorAccessor>();
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        var database = await databaseRepository.FindByIdAsync(tenantId, databaseId, cancellationToken);
        var task = await importTaskRepository.FindByIdAsync(tenantId, taskId, cancellationToken);
        if (database is null || task is null)
        {
            _logger.LogWarning("数据库批量任务缺失。databaseId={DatabaseId}, taskId={TaskId}", databaseId, taskId);
            return;
        }

        try
        {
            task.MarkRunning();
            await importTaskRepository.UpdateAsync(task, cancellationToken);

            List<string>? rows = null;
            if (!string.IsNullOrWhiteSpace(task.PayloadJson))
            {
                try
                {
                    rows = JsonSerializer.Deserialize<List<string>>(task.PayloadJson) ?? new List<string>();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "数据库批量任务 payload 解析失败。taskId={TaskId}", taskId);
                }
            }
            rows ??= new List<string>();

            if (rows.Count == 0)
            {
                task.MarkCompleted(0, 0, 0);
                await importTaskRepository.UpdateAsync(task, cancellationToken);
                return;
            }

            var validRecords = new List<AiDatabaseRecord>(rows.Count);
            var failed = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    EnsureJsonObject(rows[i]);
                    var coercedJson = AiDatabaseValueCoercer.Coerce(database.TableSchema, rows[i]);
                    validRecords.Add(new AiDatabaseRecord(
                        tenantId,
                        databaseId,
                        coercedJson,
                        idGeneratorAccessor.NextId(),
                        ownerUserId,
                        creatorUserId,
                        channelId));
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogWarning(ex, "数据库批量任务第 {Index} 行处理失败 db={DatabaseId} task={TaskId}", i, databaseId, taskId);
                }
            }

            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                if (validRecords.Count > 0)
                {
                    await recordRepository.AddRangeAsync(validRecords, cancellationToken);
                    var count = await recordRepository.CountByDatabaseAsync(tenantId, databaseId, cancellationToken);
                    database.SetRecordCount(count);
                    await databaseRepository.UpdateAsync(database, cancellationToken);
                }
            }, cancellationToken);

            task.MarkCompleted(rows.Count, validRecords.Count, failed);
            await importTaskRepository.UpdateAsync(task, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库批量任务失败。databaseId={DatabaseId}, taskId={TaskId}", databaseId, taskId);
            task.MarkFailed(ex.Message);
            await importTaskRepository.UpdateAsync(task, cancellationToken);
        }
    }

    private async Task ProcessImportAsync(
        IServiceProvider serviceProvider,
        TenantId tenantId,
        long databaseId,
        long taskId,
        long fileId,
        CancellationToken cancellationToken)
    {
        var databaseRepository = serviceProvider.GetRequiredService<AiDatabaseRepository>();
        var recordRepository = serviceProvider.GetRequiredService<AiDatabaseRecordRepository>();
        var importTaskRepository = serviceProvider.GetRequiredService<AiDatabaseImportTaskRepository>();
        var fileStorageService = serviceProvider.GetRequiredService<IFileStorageService>();
        var idGeneratorAccessor = serviceProvider.GetRequiredService<IIdGeneratorAccessor>();
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();

        var database = await databaseRepository.FindByIdAsync(tenantId, databaseId, cancellationToken);
        var task = await importTaskRepository.FindByIdAsync(tenantId, taskId, cancellationToken);
        if (database is null || task is null)
        {
            _logger.LogWarning("数据库导入任务缺失。databaseId={DatabaseId}, taskId={TaskId}", databaseId, taskId);
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

            var records = rows
                .Select(row => new AiDatabaseRecord(
                    tenantId,
                    databaseId,
                    JsonSerializer.Serialize(row),
                    idGeneratorAccessor.NextId(),
                    task.OwnerUserId,
                    task.CreatorUserId,
                    task.ChannelId))
                .ToArray();

            await unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await recordRepository.AddRangeAsync(records, cancellationToken);
                var count = await recordRepository.CountByDatabaseAsync(tenantId, databaseId, cancellationToken);
                database.SetRecordCount(count);
                await databaseRepository.UpdateAsync(database, cancellationToken);
            }, cancellationToken);

            task.MarkCompleted(rows.Count, rows.Count, 0);
            await importTaskRepository.UpdateAsync(task, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据库导入任务失败。databaseId={DatabaseId}, taskId={TaskId}", databaseId, taskId);
            task.MarkFailed(ex.Message);
            await importTaskRepository.UpdateAsync(task, cancellationToken);
        }
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
        var lines = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

    private static AiDatabaseListItem MapListItem(AiDatabase entity)
        => new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.BotId,
            entity.RecordCount,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static AiDatabaseDetail MapDetail(AiDatabase entity)
        => new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.BotId,
            entity.TableSchema,
            entity.RecordCount,
            entity.CreatedAt,
            entity.UpdatedAt);

    private static AiDatabaseRecordListItem MapRecord(AiDatabaseRecord entity)
        => new(
            entity.Id,
            entity.DatabaseId,
            entity.DataJson,
            entity.CreatedAt,
            entity.UpdatedAt);
}
