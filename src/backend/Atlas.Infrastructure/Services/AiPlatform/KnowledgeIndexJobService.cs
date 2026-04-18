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
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// 索引任务专用服务（v5 §35 / 计划 G3 + G6）。
/// 写入 <see cref="KnowledgeIndexJob"/> 表 + Hangfire 调度 <see cref="KnowledgeIndexJobRunner"/>。
/// 支持 append / overwrite 模式：overwrite 模式下，runner 在重新切片前先 GC 旧 chunks 与向量。
/// </summary>
public sealed class KnowledgeIndexJobService : IKnowledgeIndexJobService
{
    private readonly KnowledgeIndexJobRepository _indexJobRepository;
    private readonly KnowledgeJobRepository _legacyJobRepository;
    private readonly KnowledgeBaseRepository _knowledgeBaseRepository;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ILogger<KnowledgeIndexJobService> _logger;

    public KnowledgeIndexJobService(
        KnowledgeIndexJobRepository indexJobRepository,
        KnowledgeJobRepository legacyJobRepository,
        KnowledgeBaseRepository knowledgeBaseRepository,
        IBackgroundJobClient backgroundJobs,
        IIdGeneratorAccessor idGeneratorAccessor,
        ILogger<KnowledgeIndexJobService> logger)
    {
        _indexJobRepository = indexJobRepository;
        _legacyJobRepository = legacyJobRepository;
        _knowledgeBaseRepository = knowledgeBaseRepository;
        _backgroundJobs = backgroundJobs;
        _idGeneratorAccessor = idGeneratorAccessor;
        _logger = logger;
    }

    public async Task<long> EnqueueIndexAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        ChunkingProfile? chunkingProfile,
        KnowledgeIndexMode mode,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
        var jobId = _idGeneratorAccessor.NextId();
        var profileJson = chunkingProfile is null ? "{}" : JsonSerializer.Serialize(chunkingProfile, JsonOptions);
        var modeString = mode == KnowledgeIndexMode.Overwrite ? "overwrite" : "append";

        var entity = new KnowledgeIndexJob(tenantId, jobId, knowledgeBaseId, documentId, profileJson, modeString);
        await _indexJobRepository.AddAsync(entity, cancellationToken);

        var legacy = new KnowledgeJob(
            tenantId, jobId, knowledgeBaseId, KnowledgeJobService.JobTypeIndex, documentId,
            JsonSerializer.Serialize(new { chunkingProfile, mode = modeString }, JsonOptions));
        await _legacyJobRepository.AddAsync(legacy, cancellationToken);

        var hangfireId = _backgroundJobs.Enqueue<KnowledgeIndexJobRunner>(runner =>
            runner.RunAsync(tenantId.Value, knowledgeBaseId, jobId, CancellationToken.None));
        _logger.LogInformation("KnowledgeIndexJob enqueued: jobId={JobId} mode={Mode} hangfireId={HangfireId}", jobId, modeString, hangfireId);
        return jobId;
    }

    public async Task<IReadOnlyList<IndexJobDto>> ListByDocumentAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        CancellationToken cancellationToken)
    {
        await EnsureKnowledgeBaseAsync(tenantId, knowledgeBaseId, cancellationToken);
        var entities = await _indexJobRepository.ListByDocumentAsync(tenantId, knowledgeBaseId, documentId, cancellationToken);
        return entities.Select(Map).ToList();
    }

    public async Task<long> RebuildAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        long documentId,
        IndexJobRebuildRequest request,
        CancellationToken cancellationToken)
    {
        return await EnqueueIndexAsync(tenantId, knowledgeBaseId, documentId, request.ChunkingProfile, request.Mode, cancellationToken);
    }

    private async Task EnsureKnowledgeBaseAsync(TenantId tenantId, long knowledgeBaseId, CancellationToken cancellationToken)
    {
        var kb = await _knowledgeBaseRepository.FindByIdAsync(tenantId, knowledgeBaseId, cancellationToken);
        if (kb is null)
        {
            throw new BusinessException("知识库不存在。", ErrorCodes.NotFound);
        }
    }

    private static IndexJobDto Map(KnowledgeIndexJob entity)
    {
        ChunkingProfile? profile = null;
        if (!string.IsNullOrWhiteSpace(entity.ChunkingProfileJson) && entity.ChunkingProfileJson.Trim() != "{}")
        {
            try { profile = JsonSerializer.Deserialize<ChunkingProfile>(entity.ChunkingProfileJson, JsonOptions); }
            catch (JsonException) { profile = null; }
        }

        var mode = entity.Mode?.ToLowerInvariant() == "overwrite"
            ? KnowledgeIndexMode.Overwrite
            : KnowledgeIndexMode.Append;

        return new IndexJobDto(
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
            profile,
            mode);
    }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

/// <summary>
/// Hangfire 索引作业入口（v5 §35 / 计划 G3）。
/// overwrite 模式下：先用 <see cref="DocumentChunkRepository"/> + <see cref="IVectorStore"/> 清空旧 chunks，再走标准处理管线。
/// </summary>
[AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
[DisableConcurrentExecution(timeoutInSeconds: 60)]
public sealed class KnowledgeIndexJobRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KnowledgeIndexJobRunner> _logger;

    public KnowledgeIndexJobRunner(IServiceScopeFactory scopeFactory, ILogger<KnowledgeIndexJobRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RunAsync(Guid tenantGuid, long knowledgeBaseId, long jobId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var tenantId = new TenantId(tenantGuid);
        var indexRepo = scope.ServiceProvider.GetRequiredService<KnowledgeIndexJobRepository>();
        var legacyRepo = scope.ServiceProvider.GetRequiredService<KnowledgeJobRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<DocumentProcessingService>();
        var chunkRepo = scope.ServiceProvider.GetRequiredService<DocumentChunkRepository>();
        var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();

        var entity = await indexRepo.FindByIdAsync(tenantId, jobId, cancellationToken);
        if (entity is null)
        {
            _logger.LogWarning("KnowledgeIndexJobRunner: job not found jobId={JobId}", jobId);
            return;
        }
        var legacy = await legacyRepo.FindByIdAsync(tenantId, jobId, cancellationToken);

        try
        {
            entity.Start(DateTime.UtcNow, hangfireJobId: null);
            entity.Update("Running", progress: 5);
            await indexRepo.UpdateAsync(entity, cancellationToken);
            if (legacy is not null) { legacy.Start(DateTime.UtcNow, null); legacy.Update("Running", 5); await legacyRepo.UpdateAsync(legacy, cancellationToken); }

            // overwrite 模式：先 GC 旧 chunks + 向量
            if (string.Equals(entity.Mode, "overwrite", StringComparison.OrdinalIgnoreCase))
            {
                var existingChunks = await chunkRepo.GetAllByDocumentAsync(tenantId, entity.DocumentId, cancellationToken);
                var existingIds = existingChunks.Select(x => x.Id.ToString()).ToList();
                await chunkRepo.DeleteByDocumentAsync(tenantId, entity.DocumentId, cancellationToken);
                if (existingIds.Count > 0)
                {
                    try { await vectorStore.DeleteAsync($"kb_{entity.KnowledgeBaseId}", existingIds, cancellationToken); }
                    catch (InvalidOperationException) { /* 缺向量集合：无需处理 */ }
                }
                entity.Update("Running", progress: 25);
                await indexRepo.UpdateAsync(entity, cancellationToken);
            }

            // 解析 ChunkingProfile 并映射到 ChunkingOptions
            ChunkingProfile? profile = null;
            if (!string.IsNullOrWhiteSpace(entity.ChunkingProfileJson) && entity.ChunkingProfileJson.Trim() != "{}")
            {
                try { profile = JsonSerializer.Deserialize<ChunkingProfile>(entity.ChunkingProfileJson, KnowledgeIndexJobService.JsonOptions); }
                catch (JsonException) { profile = null; }
            }
            var options = MapOptions(profile);

            entity.Update("Running", progress: 50);
            await indexRepo.UpdateAsync(entity, cancellationToken);

            await processor.ProcessAsync(tenantId, entity.KnowledgeBaseId, entity.DocumentId, options, cancellationToken);

            entity.Finish("Succeeded", 100, errorMessage: null, DateTime.UtcNow);
            await indexRepo.UpdateAsync(entity, cancellationToken);
            if (legacy is not null) { legacy.Finish("Succeeded", 100, null, DateTime.UtcNow); await legacyRepo.UpdateAsync(legacy, cancellationToken); }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KnowledgeIndexJob failed: jobId={JobId}", jobId);
            entity.IncrementAttempts();
            var deadLetter = entity.Attempts >= entity.MaxAttempts;
            entity.Finish(deadLetter ? "DeadLetter" : "Failed", entity.Progress, ex.Message, DateTime.UtcNow);
            await indexRepo.UpdateAsync(entity, cancellationToken);
            if (legacy is not null)
            {
                legacy.IncrementAttempts();
                legacy.Finish(deadLetter ? "DeadLetter" : "Failed", entity.Progress, ex.Message, DateTime.UtcNow);
                await legacyRepo.UpdateAsync(legacy, cancellationToken);
            }
            throw;
        }
    }

    private static ChunkingOptions MapOptions(ChunkingProfile? profile)
    {
        if (profile is null) return new ChunkingOptions();
        var strategy = profile.Mode switch
        {
            ChunkingProfileMode.Semantic => ChunkingStrategy.Semantic,
            ChunkingProfileMode.TableRow => ChunkingStrategy.Fixed, // 表行切片仍按 Fixed pipeline，由 chunker 内部决定
            ChunkingProfileMode.ImageItem => ChunkingStrategy.Fixed,
            _ => ChunkingStrategy.Fixed
        };
        return new ChunkingOptions(
            ChunkSize: profile.Size > 0 ? profile.Size : 500,
            Overlap: profile.Overlap >= 0 ? profile.Overlap : 50,
            Strategy: strategy,
            ParseStrategy: DocumentParseStrategy.Quick);
    }
}
