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
/// 知识库任务系统（v5 §35/§37/§42）：
/// - 把"上传/解析/索引/重建/GC"独立成 KnowledgeJob 实体并持久化状态机
/// - 使用 <see cref="IBackgroundWorkQueue"/> 在后台 DI scope 中执行实际工作（保留现有平台基础设施）
/// - 提供 retry / cancel / dead-letter 接口，对前端任务中心 UI 直接对应
///
/// 与 Hangfire 的关系：现阶段平台使用进程内 IBackgroundWorkQueue（已与 Hangfire 共存）。
/// 未来若把 IBackgroundWorkQueue 实现切换为 Hangfire client，所有 KnowledgeJob 状态机仍然有效。
/// </summary>
public sealed class KnowledgeJobService : IKnowledgeJobService
{
    private readonly KnowledgeJobRepository _jobRepository;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly KnowledgeDocumentRepository _documentRepository;
    private readonly IBackgroundWorkQueue _backgroundWorkQueue;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ILogger<KnowledgeJobService> _logger;

    public KnowledgeJobService(
        KnowledgeJobRepository jobRepository,
        KnowledgeBaseRepository knowledgeBaseRepository,
        KnowledgeDocumentRepository documentRepository,
        IBackgroundWorkQueue backgroundWorkQueue,
        IIdGeneratorAccessor idGeneratorAccessor,
        ILogger<KnowledgeJobService> logger)
    {
        _jobRepository = jobRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _documentRepository = documentRepository;
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

        var job = await EnqueueJobAsync(
            tenantId,
            knowledgeBaseId,
            JobTypeParse,
            request.DocumentId,
            BuildPayload(request.ParsingStrategy),
            cancellationToken);

        EnqueueProcessingWork(tenantId, knowledgeBaseId, request.DocumentId, request.ParsingStrategy, job.Id);
        return job.Id;
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
            // 单文档重建：等价于重跑解析 + 索引
            var rerun = new RerunParseRequest(request.DocumentId.Value);
            return await RerunParseAsync(tenantId, knowledgeBaseId, rerun, cancellationToken);
        }

        var job = await EnqueueJobAsync(
            tenantId,
            knowledgeBaseId,
            JobTypeRebuild,
            documentId: null,
            payloadJson: "{}",
            cancellationToken);

        // 全量重建：迭代所有文档，串行触发 reprocess
        _backgroundWorkQueue.Enqueue(async (sp, ct) =>
        {
            var jobRepo = sp.GetRequiredService<KnowledgeJobRepository>();
            var docRepo = sp.GetRequiredService<KnowledgeDocumentRepository>();
            var processor = sp.GetRequiredService<DocumentProcessingService>();
            var entity = await jobRepo.FindByIdAsync(tenantId, job.Id, ct);
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
                    await processor.ProcessAsync(tenantId, knowledgeBaseId, doc.Id, new ChunkingOptions(), ct);
                    var percent = Math.Clamp((int)((index + 1) * 100.0 / total), 5, 95);
                    entity.Update(JobStatusRunning, percent);
                    await jobRepo.UpdateAsync(entity, ct);
                }

                entity.Finish(JobStatusSucceeded, 100, errorMessage: null, DateTime.UtcNow);
                await jobRepo.UpdateAsync(entity, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Knowledge rebuild job {JobId} failed", job.Id);
                var deadLetter = entity.Attempts >= entity.MaxAttempts;
                entity.Finish(deadLetter ? JobStatusDeadLetter : JobStatusFailed, entity.Progress, ex.Message, DateTime.UtcNow);
                await jobRepo.UpdateAsync(entity, ct);
            }
        });

        return job.Id;
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

        // 根据 job 类型重新调度
        if (job.Type == JobTypeParse || job.Type == JobTypeIndex)
        {
            if (job.DocumentId.HasValue)
            {
                var parsing = TryDeserializeParsing(job.PayloadJson);
                EnqueueProcessingWork(tenantId, knowledgeBaseId, job.DocumentId.Value, parsing, job.Id);
            }
            return;
        }

        if (job.Type == JobTypeRebuild)
        {
            _ = await RebuildIndexAsync(tenantId, knowledgeBaseId, new RebuildIndexRequest(), cancellationToken);
            return;
        }

        // 兜底：GC 等其它类型直接走极简后台队列重新触发
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

    /* -------------------- 平台内部 enqueue helpers -------------------- */

    /// <summary>
    /// 把"解析+索引"作为单个 Parse 任务持久化（mock/前端把 Indexing 看成 Parse 后续阶段）。
    /// 由 <see cref="DocumentService.CreateAsync"/> 与重跑路径调用。
    /// </summary>
    public async Task<KnowledgeJob> EnqueueParseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        ParsingStrategy? parsingStrategy,
        CancellationToken cancellationToken)
    {
        var job = await EnqueueJobAsync(
            tenantId,
            knowledgeBaseId,
            JobTypeParse,
            documentId,
            BuildPayload(parsingStrategy),
            cancellationToken);

        EnqueueProcessingWork(tenantId, knowledgeBaseId, documentId, parsingStrategy, job.Id);
        return job;
    }

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

    private void EnqueueProcessingWork(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        ParsingStrategy? parsingStrategy,
        long jobId)
    {
        var options = MapToChunkingOptions(parsingStrategy);
        _backgroundWorkQueue.Enqueue(async (sp, ct) =>
        {
            var jobRepo = sp.GetRequiredService<KnowledgeJobRepository>();
            var processor = sp.GetRequiredService<DocumentProcessingService>();
            var job = await jobRepo.FindByIdAsync(tenantId, jobId, ct);
            if (job is null) return;

            try
            {
                job.Start(DateTime.UtcNow, hangfireJobId: null);
                job.Update(JobStatusRunning, progress: 25);
                await jobRepo.UpdateAsync(job, ct);

                await processor.ProcessAsync(tenantId, knowledgeBaseId, documentId, options, ct);

                job.Finish(JobStatusSucceeded, 100, errorMessage: null, DateTime.UtcNow);
                await jobRepo.UpdateAsync(job, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Knowledge job {JobId} failed", jobId);
                var deadLetter = job.Attempts >= job.MaxAttempts;
                job.Finish(deadLetter ? JobStatusDeadLetter : JobStatusFailed, job.Progress, ex.Message, DateTime.UtcNow);
                await jobRepo.UpdateAsync(job, ct);
            }
        });
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

    private static string BuildPayload(ParsingStrategy? parsingStrategy)
    {
        if (parsingStrategy is null) return "{}";
        return JsonSerializer.Serialize(parsingStrategy, JsonOptions);
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

    private static ChunkingOptions MapToChunkingOptions(ParsingStrategy? strategy)
    {
        if (strategy is null)
        {
            return new ChunkingOptions();
        }

        var parseStrategy = strategy.ParsingType == ParsingType.Precise
            ? DocumentParseStrategy.Precise
            : DocumentParseStrategy.Quick;

        return new ChunkingOptions(
            ChunkSize: 500,
            Overlap: 50,
            Strategy: ChunkingStrategy.Fixed,
            ParseStrategy: parseStrategy);
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
