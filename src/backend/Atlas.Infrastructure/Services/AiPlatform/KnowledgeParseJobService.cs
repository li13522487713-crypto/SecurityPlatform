using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions.Knowledge;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities.Knowledge;
using Atlas.Infrastructure.Repositories;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// 解析任务专用服务（v5 §35 / 计划 G3）。
/// 通过 <see cref="IBackgroundJobClient"/> 把 <see cref="KnowledgeParseJobRunner.RunAsync"/> 真正下发到 Hangfire。
/// 失败时由 Hangfire <c>[AutomaticRetry]</c> + <see cref="KnowledgeJobStateFilter"/> 联合接管 Attempts/DeadLetter 转移。
/// </summary>
public sealed class KnowledgeParseJobService : IKnowledgeParseJobService
{
    private readonly KnowledgeParseJobRepository _parseJobRepository;
    private readonly KnowledgeJobRepository _legacyJobRepository;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly KnowledgeDocumentRepository _documentRepository;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ILogger<KnowledgeParseJobService> _logger;

    public KnowledgeParseJobService(
        KnowledgeParseJobRepository parseJobRepository,
        KnowledgeJobRepository legacyJobRepository,
        KnowledgeBaseRepository knowledgeBaseRepository,
        KnowledgeDocumentRepository documentRepository,
        IBackgroundJobClient backgroundJobs,
        IIdGeneratorAccessor idGeneratorAccessor,
        ILogger<KnowledgeParseJobService> logger)
    {
        _parseJobRepository = parseJobRepository;
        _legacyJobRepository = legacyJobRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _documentRepository = documentRepository;
        _backgroundJobs = backgroundJobs;
        _idGeneratorAccessor = idGeneratorAccessor;
        _logger = logger;
    }

    public async Task<long> EnqueueParseAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        ParsingStrategy? parsingStrategy,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
        var jobId = _idGeneratorAccessor.NextId();
        var payloadJson = parsingStrategy is null ? "{}" : JsonSerializer.Serialize(parsingStrategy, JsonOptions);
        var entity = new KnowledgeParseJob(tenantId, jobId, knowledgeBaseId, documentId, payloadJson);
        await _parseJobRepository.AddAsync(entity, cancellationToken);

        // 兼容写入旧 KnowledgeJob 表，确保 jobs 列表 / 跨 KB 查询 / 死信计数继续工作
        var legacy = new KnowledgeJob(tenantId, jobId, knowledgeBaseId, KnowledgeJobService.JobTypeParse, documentId, payloadJson);
        await _legacyJobRepository.AddAsync(legacy, cancellationToken);

        var hangfireId = _backgroundJobs.Enqueue<KnowledgeParseJobRunner>(runner =>
            runner.RunAsync(tenantId.Value, knowledgeBaseId, jobId, CancellationToken.None));
        _logger.LogInformation("KnowledgeParseJob enqueued: jobId={JobId} hangfireId={HangfireId}", jobId, hangfireId);
        return jobId;
    }

    public async Task<IReadOnlyList<ParseJobDto>> ListByDocumentAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
        var entities = await _parseJobRepository.ListByDocumentAsync(tenantId, knowledgeBaseId, documentId, cancellationToken);
        return entities.Select(Map).ToList();
    }

    public async Task<long> ReplayAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        ParseJobReplayRequest request,
        CancellationToken cancellationToken)
    {
        return await EnqueueParseAsync(tenantId, knowledgeBaseId, documentId, request.ParsingStrategy, cancellationToken);
    }

    private async Task EnsureKnowledgeBaseAsync(TenantId tenantId, long knowledgeBaseId, CancellationToken cancellationToken)
    {
        var kb = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken);
        if (kb is null)
        {
            throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        }
    }

    private static ParseJobDto Map(KnowledgeParseJob entity)
    {
        ParsingStrategy? strategy = null;
        if (!string.IsNullOrWhiteSpace(entity.ParsingStrategyJson) && entity.ParsingStrategyJson.Trim() != "{}")
        {
            try
            {
                strategy = JsonSerializer.Deserialize<ParsingStrategy>(entity.ParsingStrategyJson, JsonOptions);
            }
            catch (JsonException) { strategy = null; }
        }

        return new ParseJobDto(
            entity.Id,
            entity.KnowledgeBaseId,
            entity.DocumentId,
            KnowledgeJobService.ParseJobStatus(entity.Status),
            entity.Progress,
            entity.Attempts,
            entity.MaxAttempts,
            entity.EnqueuedAt,
            entity.ErrorMessage,
            entity.StartedAt,
            entity.FinishedAt,
            strategy);
    }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

/// <summary>
/// Hangfire 作业入口（v5 §35 / 计划 G3）。
/// 自动重试由 <see cref="AutomaticRetryAttribute"/> 控制；状态机与死信由 <see cref="KnowledgeJobStateFilter"/> 监听 Hangfire FailedState 同步到 KnowledgeJob/KnowledgeParseJob。
/// </summary>
[AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
[DisableConcurrentExecution(timeoutInSeconds: 60)]
public sealed class KnowledgeParseJobRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KnowledgeParseJobRunner> _logger;

    public KnowledgeParseJobRunner(IServiceScopeFactory scopeFactory, ILogger<KnowledgeParseJobRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RunAsync(Guid tenantGuid, long knowledgeBaseId, long jobId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var tenantId = new TenantId(tenantGuid);
        var parseRepo = scope.ServiceProvider.GetRequiredService<KnowledgeParseJobRepository>();
        var legacyRepo = scope.ServiceProvider.GetRequiredService<KnowledgeJobRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<DocumentProcessingService>();
        var docMetaRepo = scope.ServiceProvider.GetRequiredService<KnowledgeDocumentMetaRepository>();
        var docRepo = scope.ServiceProvider.GetRequiredService<KnowledgeDocumentRepository>();
        var idGen = scope.ServiceProvider.GetRequiredService<IIdGeneratorAccessor>();

        var entity = await parseRepo.FindByIdAsync(tenantId, jobId, cancellationToken);
        if (entity is null)
        {
            _logger.LogWarning("KnowledgeParseJobRunner: job not found jobId={JobId}", jobId);
            return;
        }

        var legacy = await legacyRepo.FindByIdAsync(tenantId, jobId, cancellationToken);

        try
        {
            entity.Start(DateTime.UtcNow, hangfireJobId: null);
            entity.Update("Running", progress: 10);
            await parseRepo.UpdateAsync(entity, cancellationToken);
            if (legacy is not null)
            {
                legacy.Start(DateTime.UtcNow, hangfireJobId: null);
                legacy.Update("Running", progress: 10);
                await legacyRepo.UpdateAsync(legacy, cancellationToken);
            }

            // v5 §35：生命周期细化 Parsing → Chunking → Indexing → Ready，由 DocumentProcessingService 内部 step 写入 sidecar
            await UpsertLifecycleAsync(docMetaRepo, docRepo, idGen, tenantId, entity.KnowledgeBaseId, entity.DocumentId, "Parsing", cancellationToken);

            ParsingStrategy? strategy = null;
            if (!string.IsNullOrWhiteSpace(entity.ParsingStrategyJson) && entity.ParsingStrategyJson.Trim() != "{}")
            {
                try { strategy = JsonSerializer.Deserialize<ParsingStrategy>(entity.ParsingStrategyJson, KnowledgeParseJobService.JsonOptions); }
                catch (JsonException) { strategy = null; }
            }
            var options = MapToChunkingOptions(strategy);

            entity.Update("Running", progress: 30);
            await parseRepo.UpdateAsync(entity, cancellationToken);
            await UpsertLifecycleAsync(docMetaRepo, docRepo, idGen, tenantId, entity.KnowledgeBaseId, entity.DocumentId, "Chunking", cancellationToken);

            await processor.ProcessAsync(tenantId, knowledgeBaseId, entity.DocumentId, options, cancellationToken);

            entity.Update("Running", progress: 80);
            await parseRepo.UpdateAsync(entity, cancellationToken);
            await UpsertLifecycleAsync(docMetaRepo, docRepo, idGen, tenantId, entity.KnowledgeBaseId, entity.DocumentId, "Indexing", cancellationToken);

            entity.Finish("Succeeded", 100, errorMessage: null, DateTime.UtcNow);
            await parseRepo.UpdateAsync(entity, cancellationToken);
            if (legacy is not null)
            {
                legacy.Finish("Succeeded", 100, errorMessage: null, DateTime.UtcNow);
                await legacyRepo.UpdateAsync(legacy, cancellationToken);
            }
            await UpsertLifecycleAsync(docMetaRepo, docRepo, idGen, tenantId, entity.KnowledgeBaseId, entity.DocumentId, "Ready", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KnowledgeParseJob failed: jobId={JobId}", jobId);
            entity.IncrementAttempts();
            var deadLetter = entity.Attempts >= entity.MaxAttempts;
            entity.Finish(deadLetter ? "DeadLetter" : "Failed", entity.Progress, ex.Message, DateTime.UtcNow);
            await parseRepo.UpdateAsync(entity, cancellationToken);
            if (legacy is not null)
            {
                legacy.IncrementAttempts();
                legacy.Finish(deadLetter ? "DeadLetter" : "Failed", entity.Progress, ex.Message, DateTime.UtcNow);
                await legacyRepo.UpdateAsync(legacy, cancellationToken);
            }
            await UpsertLifecycleAsync(docMetaRepo, docRepo, idGen, tenantId, entity.KnowledgeBaseId, entity.DocumentId, "Failed", cancellationToken);
            throw; // 让 Hangfire 看到异常，按 [AutomaticRetry] 调度后续重试
        }
    }

    private static ChunkingOptions MapToChunkingOptions(ParsingStrategy? strategy)
    {
        if (strategy is null) return new ChunkingOptions();
        var parseStrategy = strategy.ParsingType == ParsingType.Precise
            ? DocumentParseStrategy.Precise
            : DocumentParseStrategy.Quick;
        return new ChunkingOptions(
            ChunkSize: 500,
            Overlap: 50,
            Strategy: ChunkingStrategy.Fixed,
            ParseStrategy: parseStrategy);
    }

    private static async Task UpsertLifecycleAsync(
        KnowledgeDocumentMetaRepository repo,
        KnowledgeDocumentRepository docRepo,
        IIdGeneratorAccessor idGen,
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        string lifecycle,
        CancellationToken cancellationToken)
    {
        var existing = await repo.FindByDocumentAsync(tenantId, documentId, cancellationToken);
        if (existing is null)
        {
            existing = new KnowledgeDocumentMetaEntity(
                tenantId,
                documentId,
                knowledgeBaseId,
                lifecycle,
                "{}",
                parseJobId: null,
                indexJobId: null,
                versionLabel: "v0",
                ownerUserId: null);
            await repo.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.SetLifecycle(lifecycle);
            await repo.UpdateAsync(existing, cancellationToken);
        }
        _ = docRepo;
        _ = idGen;
    }
}
