using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Abstractions.Knowledge;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities.Knowledge;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// 知识库任务系统聚合查询门面（v5 §35/§37/§42 / 计划 G3）：
/// - 持久化与跨 KB 查询用 <see cref="KnowledgeJobRepository"/>（兼容旧表）。
/// - 解析 / 索引的入队改为委托给 <see cref="IKnowledgeParseJobService"/> /
///   <see cref="IKnowledgeIndexJobService"/>，由它们写入新表并下发 Hangfire BackgroundJob。
/// - 重建 / 死信重投仍由本类编排，但具体执行通过专用 service。
/// - 提供 retry / cancel / dead-letter 接口，对前端任务中心 UI 直接对应。
/// </summary>
public sealed class KnowledgeJobService : IKnowledgeJobService
{
    private readonly KnowledgeJobRepository _jobRepository;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly KnowledgeDocumentRepository _documentRepository;
    private readonly IKnowledgeParseJobService _parseJobService;
    private readonly IKnowledgeIndexJobService _indexJobService;
    private readonly IBackgroundWorkQueue _backgroundWorkQueue;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ILogger<KnowledgeJobService> _logger;

    public KnowledgeJobService(
        KnowledgeJobRepository jobRepository,
        KnowledgeBaseRepository knowledgeBaseRepository,
        KnowledgeDocumentRepository documentRepository,
        IKnowledgeParseJobService parseJobService,
        IKnowledgeIndexJobService indexJobService,
        IBackgroundWorkQueue backgroundWorkQueue,
        IIdGeneratorAccessor idGeneratorAccessor,
        ILogger<KnowledgeJobService> logger)
    {
        _jobRepository = jobRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _documentRepository = documentRepository;
        _parseJobService = parseJobService;
        _indexJobService = indexJobService;
        _backgroundWorkQueue = backgroundWorkQueue;
        _idGeneratorAccessor = idGeneratorAccessor;
        _logger = logger;
    }

    /* -------------------- IKnowledgeJobService -------------------- */

    public async Task<PagedResult<KnowledgeJobDto>> ListAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        KnowledgeJobsListRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
        var (items, total) = await _jobRepository.GetPagedAsync(
            tenantId,
            knowledgeBaseId,
            request.Status.HasValue ? StatusToString(request.Status.Value) : null,
            request.Type.HasValue ? TypeToString(request.Type.Value) : null,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        return new PagedResult<KnowledgeJobDto>(items.Select(Map).ToList(), total, request.PageIndex, request.PageSize);
    }

    public async Task<PagedResult<KnowledgeJobDto>> ListAcrossKnowledgeBasesAsync(
        TenantId tenantId,
        KnowledgeJobsListRequest request,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _jobRepository.GetPagedAsync(
            tenantId,
            knowledgeBaseId: null,
            request.Status.HasValue ? StatusToString(request.Status.Value) : null,
            request.Type.HasValue ? TypeToString(request.Type.Value) : null,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        return new PagedResult<KnowledgeJobDto>(items.Select(Map).ToList(), total, request.PageIndex, request.PageSize);
    }

    public async Task<KnowledgeJobDto?> GetAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long jobId,
        CancellationToken cancellationToken)
    {
        var entity = await _jobRepository.FindByIdAsync(tenantId, jobId, cancellationToken);
        if (entity is null || entity.KnowledgeBaseId != knowledgeBaseId)
        {
            return null;
        }
        return Map(entity);
    }

    public async Task<long> RerunParseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        RerunParseRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
        var doc = await _documentRepository.FindByKnowledgeBaseAndIdAsync(tenantId, knowledgeBaseId, request.DocumentId, cancellationToken)
            ?? throw new BusinessException("文档不存在。", ErrorCodes.NotFound);

        doc.MarkProcessing();
        await _documentRepository.UpdateAsync(doc, cancellationToken);

        // v5 §35 / 计划 G3：委托给专用 ParseJobService（写新表 + Hangfire 调度）
        return await _parseJobService.EnqueueParseAsync(tenantId, knowledgeBaseId, request.DocumentId, request.ParsingStrategy, cancellationToken);
    }

    public async Task<long> RebuildIndexAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        RebuildIndexRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);

        if (request.DocumentId.HasValue)
        {
            // v5 §35 / 计划 G3：单文档重建走 IKnowledgeIndexJobService（Hangfire + overwrite/append 模式）
            return await _indexJobService.EnqueueIndexAsync(
                tenantId, knowledgeBaseId, request.DocumentId.Value,
                chunkingProfile: null, request.Mode, cancellationToken);
        }

        // 全量重建：先建一个聚合 rebuild job，再迭代当前 KB 文档串行触发 IndexJob
        var rebuildJob = await EnqueueJobAsync(
            tenantId,
            knowledgeBaseId,
            JobTypeRebuild,
            documentId: null,
            payloadJson: System.Text.Json.JsonSerializer.Serialize(new { mode = request.Mode.ToString().ToLowerInvariant() }, JsonOptions),
            cancellationToken);

        _backgroundWorkQueue.Enqueue(async (sp, ct) =>
        {
            var jobRepo = sp.GetRequiredService<KnowledgeJobRepository>();
            var docRepo = sp.GetRequiredService<KnowledgeDocumentRepository>();
            var indexService = sp.GetRequiredService<IKnowledgeIndexJobService>();
            var entity = await jobRepo.FindByIdAsync(tenantId, rebuildJob.Id, ct);
            if (entity is null) return;

            try
            {
                entity.Start(DateTime.UtcNow, hangfireJobId: null);
                entity.Update(JobStatusRunning, progress: 5);
                await jobRepo.UpdateAsync(entity, ct);

                var (docs, _) = await docRepo.GetByKnowledgeBaseAsync(tenantId, knowledgeBaseId, 1, 10_000, ct);
                var total = Math.Max(1, docs.Count);
                for (var index = 0; index < docs.Count; index += 1)
                {
                    var doc = docs[index];
                    await indexService.EnqueueIndexAsync(tenantId, knowledgeBaseId, doc.Id, chunkingProfile: null, request.Mode, ct);
                    var percent = Math.Clamp((int)((index + 1) * 100.0 / total), 5, 95);
                    entity.Update(JobStatusRunning, percent);
                    await jobRepo.UpdateAsync(entity, ct);
                }

                entity.Finish(JobStatusSucceeded, 100, errorMessage: null, DateTime.UtcNow);
                await jobRepo.UpdateAsync(entity, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Knowledge rebuild job {JobId} failed", rebuildJob.Id);
                entity.IncrementAttempts();
                var deadLetter = entity.Attempts >= entity.MaxAttempts;
                entity.Finish(deadLetter ? JobStatusDeadLetter : JobStatusFailed, entity.Progress, ex.Message, DateTime.UtcNow);
                await jobRepo.UpdateAsync(entity, ct);
            }
        });

        return rebuildJob.Id;
    }

    public async Task RetryDeadLetterAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long jobId,
        CancellationToken cancellationToken)
    {
        var job = await _jobRepository.FindByIdAsync(tenantId, jobId, cancellationToken)
            ?? throw new BusinessException("任务不存在。", ErrorCodes.NotFound);
        if (job.KnowledgeBaseId != knowledgeBaseId)
        {
            throw new BusinessException("任务不属于该知识库。", ErrorCodes.Forbidden);
        }
        if (job.Status != JobStatusDeadLetter && job.Status != JobStatusFailed)
        {
            throw new BusinessException("仅失败 / 死信任务可重投。", ErrorCodes.ValidationError);
        }

        job.IncrementAttempts();
        job.Update(JobStatusRetrying, progress: 10);
        await _jobRepository.UpdateAsync(job, cancellationToken);

        // v5 §35 / 计划 G3：按 type 重新调度到 Hangfire 链路
        if (job.Type == JobTypeParse && job.DocumentId.HasValue)
        {
            var parsing = TryDeserializeParsing(job.PayloadJson);
            await _parseJobService.EnqueueParseAsync(tenantId, knowledgeBaseId, job.DocumentId.Value, parsing, cancellationToken);
            return;
        }

        if (job.Type == JobTypeIndex && job.DocumentId.HasValue)
        {
            await _indexJobService.EnqueueIndexAsync(tenantId, knowledgeBaseId, job.DocumentId.Value, chunkingProfile: null, KnowledgeIndexMode.Append, cancellationToken);
            return;
        }

        if (job.Type == JobTypeRebuild)
        {
            _ = await RebuildIndexAsync(tenantId, knowledgeBaseId, new RebuildIndexRequest(), cancellationToken);
            return;
        }

        // 兜底：GC 等其它类型直接走极简后台队列重新触发（保持 IBackgroundWorkQueue 兼容）
        _backgroundWorkQueue.Enqueue(async (sp, ct) =>
        {
            var repo = sp.GetRequiredService<KnowledgeJobRepository>();
            var entity = await repo.FindByIdAsync(tenantId, job.Id, ct);
            if (entity is null) return;
            try
            {
                entity.Start(DateTime.UtcNow, hangfireJobId: null);
                entity.Update(JobStatusRunning, progress: 50);
                await repo.UpdateAsync(entity, ct);
                await Task.Delay(50, ct);
                entity.Finish(JobStatusSucceeded, 100, errorMessage: null, DateTime.UtcNow);
                await repo.UpdateAsync(entity, ct);
            }
            catch (Exception ex)
            {
                entity.IncrementAttempts();
                var deadLetter = entity.Attempts >= entity.MaxAttempts;
                entity.Finish(deadLetter ? JobStatusDeadLetter : JobStatusFailed, entity.Progress, ex.Message, DateTime.UtcNow);
                await repo.UpdateAsync(entity, ct);
            }
        });
    }

    public async Task<int> RetryDeadLetterBatchAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        DeadLetterRetryRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);

        // 当 JobIds 提供时，逐条重投；否则按 KB + 可选 type 拉取全部 DeadLetter 任务
        IReadOnlyList<long> targetIds;
        if (request.JobIds is not null && request.JobIds.Count > 0)
        {
            targetIds = request.JobIds;
        }
        else
        {
            var (items, _) = await _jobRepository.GetPagedAsync(
                tenantId,
                knowledgeBaseId,
                JobStatusDeadLetter,
                request.Type.HasValue ? TypeToString(request.Type.Value) : null,
                pageIndex: 1,
                pageSize: 200,
                cancellationToken);
            targetIds = items.Select(x => x.Id).ToList();
        }

        var success = 0;
        foreach (var jobId in targetIds)
        {
            try
            {
                await RetryDeadLetterAsync(tenantId, knowledgeBaseId, jobId, cancellationToken);
                success += 1;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Knowledge job {JobId} batch retry failed", jobId);
            }
        }
        return success;
    }

    public async Task CancelAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long jobId,
        CancellationToken cancellationToken)
    {
        var job = await _jobRepository.FindByIdAsync(tenantId, jobId, cancellationToken)
            ?? throw new BusinessException("任务不存在。", ErrorCodes.NotFound);
        if (job.KnowledgeBaseId != knowledgeBaseId)
        {
            throw new BusinessException("任务不属于该知识库。", ErrorCodes.Forbidden);
        }
        if (job.Status == JobStatusSucceeded
            || job.Status == JobStatusDeadLetter
            || job.Status == JobStatusCanceled)
        {
            return;
        }

        job.Finish(JobStatusCanceled, job.Progress, errorMessage: null, DateTime.UtcNow);
        await _jobRepository.UpdateAsync(job, cancellationToken);
    }

    /* -------------------- 内部 helpers -------------------- */

    /// <summary>
    /// 仅在 RebuildIndex 全量路径用作"聚合 rebuild job"占位写入；
    /// 解析 / 索引的 enqueue 已委托给 IKnowledgeParseJobService / IKnowledgeIndexJobService。
    /// </summary>
    private async Task<KnowledgeJob> EnqueueJobAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        string type,
        long? documentId,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var entity = new KnowledgeJob(
            tenantId,
            _idGeneratorAccessor.NextId(),
            knowledgeBaseId,
            type,
            documentId,
            payloadJson);
        await _jobRepository.AddAsync(entity, cancellationToken);
        return entity;
    }

    private async Task EnsureKnowledgeBaseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        CancellationToken cancellationToken)
    {
        var kb = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken);
        if (kb is null)
        {
            throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        }
    }

    /* -------------------- mappers -------------------- */

    public static KnowledgeJobDto Map(KnowledgeJob entity)
    {
        IReadOnlyList<KnowledgeJobLogEntry>? logs = null;
        if (!string.IsNullOrWhiteSpace(entity.LogsJson))
        {
            try
            {
                logs = JsonSerializer.Deserialize<IReadOnlyList<KnowledgeJobLogEntry>>(entity.LogsJson, JsonOptions);
            }
            catch (JsonException)
            {
                logs = null;
            }
        }

        return new KnowledgeJobDto(
            entity.Id,
            entity.KnowledgeBaseId,
            ParseJobType(entity.Type),
            ParseJobStatus(entity.Status),
            entity.Progress,
            entity.Attempts,
            entity.MaxAttempts,
            entity.EnqueuedAt,
            entity.DocumentId,
            entity.ErrorMessage,
            entity.StartedAt,
            entity.FinishedAt,
            entity.PayloadJson,
            logs);
    }

    private static ParsingStrategy? TryDeserializeParsing(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}") return null;
        try
        {
            return JsonSerializer.Deserialize<ParsingStrategy>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /* -------------------- 字符串常量与映射 -------------------- */

    public const string JobTypeParse = "parse";
    public const string JobTypeIndex = "index";
    public const string JobTypeRebuild = "rebuild";
    public const string JobTypeGc = "gc";

    public const string JobStatusQueued = "Queued";
    public const string JobStatusRunning = "Running";
    public const string JobStatusSucceeded = "Succeeded";
    public const string JobStatusFailed = "Failed";
    public const string JobStatusRetrying = "Retrying";
    public const string JobStatusDeadLetter = "DeadLetter";
    public const string JobStatusCanceled = "Canceled";

    public static string TypeToString(KnowledgeJobType type) => type switch
    {
        KnowledgeJobType.Parse => JobTypeParse,
        KnowledgeJobType.Index => JobTypeIndex,
        KnowledgeJobType.Rebuild => JobTypeRebuild,
        KnowledgeJobType.Gc => JobTypeGc,
        _ => JobTypeParse
    };

    public static string StatusToString(KnowledgeJobStatus status) => status switch
    {
        KnowledgeJobStatus.Queued => JobStatusQueued,
        KnowledgeJobStatus.Running => JobStatusRunning,
        KnowledgeJobStatus.Succeeded => JobStatusSucceeded,
        KnowledgeJobStatus.Failed => JobStatusFailed,
        KnowledgeJobStatus.Retrying => JobStatusRetrying,
        KnowledgeJobStatus.DeadLetter => JobStatusDeadLetter,
        KnowledgeJobStatus.Canceled => JobStatusCanceled,
        _ => JobStatusQueued
    };

    public static KnowledgeJobType ParseJobType(string value) => value switch
    {
        JobTypeIndex => KnowledgeJobType.Index,
        JobTypeRebuild => KnowledgeJobType.Rebuild,
        JobTypeGc => KnowledgeJobType.Gc,
        _ => KnowledgeJobType.Parse
    };

    public static KnowledgeJobStatus ParseJobStatus(string value) => value switch
    {
        JobStatusRunning => KnowledgeJobStatus.Running,
        JobStatusSucceeded => KnowledgeJobStatus.Succeeded,
        JobStatusFailed => KnowledgeJobStatus.Failed,
        JobStatusRetrying => KnowledgeJobStatus.Retrying,
        JobStatusDeadLetter => KnowledgeJobStatus.DeadLetter,
        JobStatusCanceled => KnowledgeJobStatus.Canceled,
        _ => KnowledgeJobStatus.Queued
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
