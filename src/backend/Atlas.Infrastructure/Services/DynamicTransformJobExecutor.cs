using System.Diagnostics;
using System.Text.Json;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicViews.Abstractions;
using Atlas.Application.DynamicViews.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicViews.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicTransformJobExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ISqlSugarClient _db;
    private readonly IDynamicViewQueryService _viewQueryService;
    private readonly IDynamicRecordCommandService _recordCommandService;
    private readonly IDynamicTableQueryService _tableQueryService;
    private readonly TimeProvider _timeProvider;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public DynamicTransformJobExecutor(
        ISqlSugarClient db,
        IDynamicViewQueryService viewQueryService,
        IDynamicRecordCommandService recordCommandService,
        IDynamicTableQueryService tableQueryService,
        TimeProvider timeProvider,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _db = db;
        _viewQueryService = viewQueryService;
        _recordCommandService = recordCommandService;
        _tableQueryService = tableQueryService;
        _timeProvider = timeProvider;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public Task ExecuteScheduledAsync(string tenantIdValue, long? appId, string jobKey)
    {
        return ExecuteAsync(tenantIdValue, appId, jobKey, null, 0, "schedule");
    }

    public async Task ExecuteAsync(string tenantIdValue, long? appId, string jobKey, long? executionId, long userId, string triggerType)
    {
        var tenantId = new TenantId(Guid.Parse(tenantIdValue));
        var now = _timeProvider.GetUtcNow();
        var job = await FindJobAsync(tenantId, appId, jobKey);
        if (job is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTransformJobNotFound");
        }

        var execution = executionId.HasValue
            ? await _db.Queryable<DynamicTransformExecution>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.Id == executionId.Value && x.JobKey == jobKey)
                .FirstAsync()
            : null;

        if (execution is null)
        {
            execution = new DynamicTransformExecution(
                tenantId,
                _idGeneratorAccessor.NextId(),
                appId,
                jobKey,
                "Running",
                triggerType,
                0,
                0,
                0,
                0,
                null,
                userId,
                now,
                null,
                "Running");
            await _db.Insertable(execution).ExecuteCommandAsync();
        }

        var sw = Stopwatch.StartNew();
        var outputRows = 0;
        var failedRows = 0;
        var inputRows = 0;
        string? error = null;
        try
        {
            var source = ParseSourceConfig(job.SourceConfigJson);
            var target = ParseTargetConfig(job.TargetConfigJson);
            if (source.Type != "view")
            {
                throw new BusinessException(ErrorCodes.ValidationError, "DynamicTransformSourceOnlyViewSupported");
            }

            var records = await _viewQueryService.QueryRecordsAsync(
                tenantId,
                appId,
                source.ViewKey,
                new DynamicViewRecordsQueryRequest(1, 1000, null, null, false, null),
                CancellationToken.None);
            inputRows = records.Items.Count;

            var targetFields = await _tableQueryService.GetFieldsAsync(tenantId, target.TableKey, appId, CancellationToken.None);
            var targetFieldNames = targetFields.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var row in records.Items)
            {
                try
                {
                    var mapped = MapValues(row, targetFieldNames);
                    if (mapped.Count == 0)
                    {
                        failedRows += 1;
                        continue;
                    }

                    await _recordCommandService.CreateAsync(
                        tenantId,
                        userId,
                        target.TableKey,
                        new DynamicRecordUpsertRequest(mapped),
                        CancellationToken.None);
                    outputRows += 1;
                }
                catch
                {
                    failedRows += 1;
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
        finally
        {
            sw.Stop();
        }

        var endedAt = _timeProvider.GetUtcNow();
        var status = string.IsNullOrWhiteSpace(error)
            ? (failedRows > 0 ? "PartiallySucceeded" : "Succeeded")
            : "Failed";
        var message = string.IsNullOrWhiteSpace(error)
            ? $"Transform completed. output={outputRows}, failed={failedRows}"
            : error;

        var executionUpdate = new DynamicTransformExecution(
            tenantId,
            execution.Id,
            appId,
            jobKey,
            status,
            triggerType,
            inputRows,
            outputRows,
            failedRows,
            sw.ElapsedMilliseconds,
            string.IsNullOrWhiteSpace(error) ? null : JsonSerializer.Serialize(new { error }, JsonOptions),
            userId,
            execution.StartedAt,
            endedAt,
            message);
        await _db.Updateable(executionUpdate)
            .UpdateColumns(x => new
            {
                x.Status,
                x.InputRows,
                x.OutputRows,
                x.FailedRows,
                x.DurationMs,
                x.ErrorDetailJson,
                x.EndedAt,
                x.Message
            })
            .Where(x => x.Id == execution.Id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync();

        job.MarkRunCompleted(userId, endedAt, status, error);
        await _db.Updateable(job)
            .Where(x => x.Id == job.Id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync();
    }

    private async Task<DynamicTransformJob?> FindJobAsync(TenantId tenantId, long? appId, string jobKey)
    {
        var query = _db.Queryable<DynamicTransformJob>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.JobKey == jobKey);
        query = appId.HasValue ? query.Where(x => x.AppId == appId.Value) : query.Where(x => x.AppId == null);
        return await query.FirstAsync();
    }

    private static List<DynamicFieldValueDto> MapValues(DynamicRecordDto record, HashSet<string> targetFieldNames)
    {
        var values = new List<DynamicFieldValueDto>();
        foreach (var item in record.Values)
        {
            if (!targetFieldNames.Contains(item.Field))
            {
                continue;
            }

            values.Add(item);
        }

        return values;
    }

    private static SourceConfig ParseSourceConfig(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicTransformSourceConfigInvalid");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : "view";
        var viewKey = root.TryGetProperty("viewKey", out var viewNode) ? viewNode.GetString() : null;
        if (!string.Equals(type, "view", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(viewKey))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicTransformSourceConfigInvalid");
        }

        return new SourceConfig("view", viewKey.Trim());
    }

    private static TargetConfig ParseTargetConfig(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicTransformTargetConfigInvalid");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : "table";
        var tableKey = root.TryGetProperty("tableKey", out var tableNode) ? tableNode.GetString() : null;
        if (!string.Equals(type, "table", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(tableKey))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicTransformTargetConfigInvalid");
        }

        return new TargetConfig("table", tableKey.Trim());
    }

    private sealed record SourceConfig(string Type, string ViewKey);
    private sealed record TargetConfig(string Type, string TableKey);
}
