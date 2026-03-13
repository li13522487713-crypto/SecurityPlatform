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
    private readonly IFileStorageService _fileStorageService;
    private readonly IBackgroundWorkQueue _backgroundWorkQueue;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AiDatabaseService> _logger;

    public AiDatabaseService(
        AiDatabaseRepository databaseRepository,
        AiDatabaseRecordRepository recordRepository,
        AiDatabaseImportTaskRepository importTaskRepository,
        IFileStorageService fileStorageService,
        IBackgroundWorkQueue backgroundWorkQueue,
        IIdGeneratorAccessor idGeneratorAccessor,
        IUnitOfWork unitOfWork,
        ILogger<AiDatabaseService> logger)
    {
        _databaseRepository = databaseRepository;
        _recordRepository = recordRepository;
        _importTaskRepository = importTaskRepository;
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

        var entity = new AiDatabase(
            tenantId,
            normalizedName,
            request.Description?.Trim(),
            request.BotId,
            request.TableSchema,
            _idGeneratorAccessor.NextId());
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

        entity.Update(normalizedName, request.Description?.Trim(), request.BotId, request.TableSchema);
        await _databaseRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _databaseRepository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("数据库不存在。", ErrorCodes.NotFound);

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
        CancellationToken cancellationToken)
    {
        var entity = await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        EnsureJsonObject(request.DataJson);

        var record = new AiDatabaseRecord(tenantId, databaseId, request.DataJson, _idGeneratorAccessor.NextId());
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _recordRepository.AddAsync(record, cancellationToken);
            var count = await _recordRepository.CountByDatabaseAsync(tenantId, databaseId, cancellationToken);
            entity.SetRecordCount(count);
            await _databaseRepository.UpdateAsync(entity, cancellationToken);
        }, cancellationToken);
        return record.Id;
    }

    public async Task UpdateRecordAsync(
        TenantId tenantId,
        long databaseId,
        long recordId,
        AiDatabaseRecordUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(tenantId, databaseId, cancellationToken);
        EnsureJsonObject(request.DataJson);

        var record = await _recordRepository.FindByDatabaseAndIdAsync(tenantId, databaseId, recordId, cancellationToken)
            ?? throw new BusinessException("数据库记录不存在。", ErrorCodes.NotFound);
        record.UpdateData(request.DataJson);
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
        CancellationToken cancellationToken)
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
            _idGeneratorAccessor.NextId());
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
            task.UpdatedAt);
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
                    idGeneratorAccessor.NextId()))
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
