using Atlas.Application.DynamicViews.Abstractions;
using Atlas.Application.DynamicViews.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicViews.Entities;
using Hangfire;
using SqlSugar;

namespace Atlas.Infrastructure.Services;

public sealed class DynamicTransformJobService : IDynamicTransformJobService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IRecurringJobManager _recurringJobManager;

    public DynamicTransformJobService(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGeneratorAccessor,
        TimeProvider timeProvider,
        IBackgroundJobClient backgroundJobClient,
        IRecurringJobManager recurringJobManager)
    {
        _db = db;
        _idGeneratorAccessor = idGeneratorAccessor;
        _timeProvider = timeProvider;
        _backgroundJobClient = backgroundJobClient;
        _recurringJobManager = recurringJobManager;
    }

    public async Task<IReadOnlyList<DynamicTransformJobDto>> ListAsync(TenantId tenantId, long? appId, CancellationToken cancellationToken)
    {
        var query = _db.Queryable<DynamicTransformJob>().Where(x => x.TenantIdValue == tenantId.Value);
        query = appId.HasValue ? query.Where(x => x.AppId == appId.Value) : query.Where(x => x.AppId == null);
        var list = await query.OrderBy(x => x.UpdatedAt, OrderByType.Desc).ToListAsync(cancellationToken);
        return list.Select(ToDto).ToArray();
    }

    public async Task<DynamicTransformJobDto?> GetAsync(TenantId tenantId, long? appId, string jobKey, CancellationToken cancellationToken)
    {
        var job = await FindJobAsync(tenantId, appId, jobKey, cancellationToken);
        return job is null ? null : ToDto(job);
    }

    public async Task<DynamicTransformJobDto> CreateAsync(TenantId tenantId, long userId, DynamicTransformJobCreateRequest request, CancellationToken cancellationToken)
    {
        var appId = ParseAppId(request.AppId);
        var exists = await _db.Queryable<DynamicTransformJob>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.JobKey == request.JobKey && x.AppId == appId)
            .AnyAsync();
        if (exists)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "DynamicTransformJobKeyExists");
        }

        var now = _timeProvider.GetUtcNow();
        var entity = new DynamicTransformJob(
            tenantId,
            _idGeneratorAccessor.NextId(),
            appId,
            request.JobKey.Trim(),
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.DefinitionJson) ? "{}" : request.DefinitionJson,
            userId,
            now);

        entity.UpdateDefinition(
            request.Name.Trim(),
            request.DefinitionJson,
            request.SourceConfigJson ?? "{}",
            request.TargetConfigJson ?? "{}",
            request.CronExpression,
            request.Enabled,
            userId,
            now);

        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await SyncRecurringJobAsync(entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task<DynamicTransformJobDto> UpdateAsync(
        TenantId tenantId,
        long userId,
        long? appId,
        string jobKey,
        DynamicTransformJobUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var job = await FindJobAsync(tenantId, appId, jobKey, cancellationToken);
        if (job is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTransformJobNotFound");
        }

        var now = _timeProvider.GetUtcNow();
        job.UpdateDefinition(
            request.Name.Trim(),
            request.DefinitionJson,
            request.SourceConfigJson ?? "{}",
            request.TargetConfigJson ?? "{}",
            request.CronExpression,
            request.Enabled,
            userId,
            now);

        await _db.Updateable(job)
            .Where(x => x.Id == job.Id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
        await SyncRecurringJobAsync(job, cancellationToken);
        return ToDto(job);
    }

    public async Task<DynamicTransformExecutionDto> RunAsync(TenantId tenantId, long userId, long? appId, string jobKey, CancellationToken cancellationToken)
    {
        var job = await FindJobAsync(tenantId, appId, jobKey, cancellationToken);
        if (job is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTransformJobNotFound");
        }

        var now = _timeProvider.GetUtcNow();
        job.MarkRunning(userId, now);
        await _db.Updateable(job)
            .Where(x => x.Id == job.Id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);

        var execution = new DynamicTransformExecution(
            tenantId,
            _idGeneratorAccessor.NextId(),
            job.AppId,
            job.JobKey,
            "Queued",
            "manual",
            0,
            0,
            0,
            0,
            null,
            userId,
            now,
            null,
            "Queued for execution.");
        await _db.Insertable(execution).ExecuteCommandAsync(cancellationToken);

        _backgroundJobClient.Enqueue<DynamicTransformJobExecutor>(x =>
            x.ExecuteAsync(tenantId.Value.ToString(), appId, jobKey, execution.Id, userId, "manual"));

        return ToExecutionDto(execution);
    }

    public async Task<DynamicTransformJobDto> PauseAsync(TenantId tenantId, long userId, long? appId, string jobKey, CancellationToken cancellationToken)
    {
        var job = await FindJobAsync(tenantId, appId, jobKey, cancellationToken);
        if (job is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTransformJobNotFound");
        }

        job.MarkPaused(userId, _timeProvider.GetUtcNow());
        await _db.Updateable(job)
            .Where(x => x.Id == job.Id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);

        _recurringJobManager.RemoveIfExists(BuildRecurringJobId(tenantId, appId, job.JobKey));
        return ToDto(job);
    }

    public async Task<DynamicTransformJobDto> ResumeAsync(TenantId tenantId, long userId, long? appId, string jobKey, CancellationToken cancellationToken)
    {
        var job = await FindJobAsync(tenantId, appId, jobKey, cancellationToken);
        if (job is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "DynamicTransformJobNotFound");
        }

        job.MarkResumed(userId, _timeProvider.GetUtcNow());
        await _db.Updateable(job)
            .Where(x => x.Id == job.Id && x.TenantIdValue == tenantId.Value)
            .ExecuteCommandAsync(cancellationToken);
        await SyncRecurringJobAsync(job, cancellationToken);
        return ToDto(job);
    }

    public async Task DeleteAsync(TenantId tenantId, long? appId, string jobKey, CancellationToken cancellationToken)
    {
        var job = await FindJobAsync(tenantId, appId, jobKey, cancellationToken);
        if (job is null)
        {
            return;
        }

        _recurringJobManager.RemoveIfExists(BuildRecurringJobId(tenantId, appId, job.JobKey));
        await _db.Deleteable<DynamicTransformJob>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == job.Id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DynamicTransformExecutionDto>> GetHistoryAsync(
        TenantId tenantId,
        long? appId,
        PagedRequest request,
        string jobKey,
        CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);

        var query = _db.Queryable<DynamicTransformExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.JobKey == jobKey);
        query = appId.HasValue ? query.Where(x => x.AppId == appId.Value) : query.Where(x => x.AppId == null);

        var list = await query.OrderBy(x => x.StartedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        return list.Select(ToExecutionDto).ToArray();
    }

    public async Task<DynamicTransformExecutionDto?> GetExecutionAsync(
        TenantId tenantId,
        long? appId,
        string jobKey,
        string executionId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(executionId, out var parsedId))
        {
            return null;
        }

        var query = _db.Queryable<DynamicTransformExecution>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.JobKey == jobKey && x.Id == parsedId);
        query = appId.HasValue ? query.Where(x => x.AppId == appId.Value) : query.Where(x => x.AppId == null);
        var execution = await query.FirstAsync(cancellationToken);
        return execution is null ? null : ToExecutionDto(execution);
    }

    private async Task SyncRecurringJobAsync(DynamicTransformJob job, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var recurringJobId = BuildRecurringJobId(new TenantId(job.TenantIdValue), job.AppId, job.JobKey);
        if (!job.Enabled || string.IsNullOrWhiteSpace(job.CronExpression))
        {
            _recurringJobManager.RemoveIfExists(recurringJobId);
            return;
        }

        _recurringJobManager.AddOrUpdate<DynamicTransformJobExecutor>(
            recurringJobId,
            x => x.ExecuteScheduledAsync(job.TenantIdValue.ToString(), job.AppId, job.JobKey),
            job.CronExpression);
    }

    private async Task<DynamicTransformJob?> FindJobAsync(TenantId tenantId, long? appId, string jobKey, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var query = _db.Queryable<DynamicTransformJob>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.JobKey == jobKey);
        query = appId.HasValue ? query.Where(x => x.AppId == appId.Value) : query.Where(x => x.AppId == null);
        return await query.FirstAsync();
    }

    private static string BuildRecurringJobId(TenantId tenantId, long? appId, string jobKey)
    {
        return $"dynamic-transform:{tenantId.Value}:{appId?.ToString() ?? "platform"}:{jobKey}";
    }

    private static long? ParseAppId(string appId)
    {
        return long.TryParse(appId, out var parsed) && parsed > 0 ? parsed : null;
    }

    private static DynamicTransformJobDto ToDto(DynamicTransformJob item)
    {
        return new DynamicTransformJobDto(
            item.Id.ToString(),
            item.AppId?.ToString(),
            item.JobKey,
            item.Name,
            item.Status,
            item.CronExpression,
            item.Enabled,
            item.LastRunAt,
            item.LastRunStatus,
            item.LastError,
            item.SourceConfigJson,
            item.TargetConfigJson,
            item.DefinitionJson,
            item.CreatedAt,
            item.UpdatedAt);
    }

    private static DynamicTransformExecutionDto ToExecutionDto(DynamicTransformExecution x)
    {
        return new DynamicTransformExecutionDto(
            x.Id.ToString(),
            x.JobKey,
            x.Status,
            x.TriggerType,
            x.InputRows,
            x.OutputRows,
            x.FailedRows,
            x.DurationMs,
            x.ErrorDetailJson,
            x.StartedBy,
            x.StartedAt,
            x.EndedAt,
            x.Message);
    }
}
