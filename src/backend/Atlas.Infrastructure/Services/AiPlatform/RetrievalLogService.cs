using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions.Knowledge;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities.Knowledge;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform;

/// <summary>
/// 检索日志服务（v5 §38）：把检索调用的"召回透明度"数据持久化到 KnowledgeRetrievalLog 表。
/// 前端 RetrievalTab / RetrievalLogsPanel 通过 IRetrievalLogService 直接消费。
/// </summary>
public sealed class RetrievalLogService : IRetrievalLogService
{
    private readonly KnowledgeRetrievalLogRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ILogger<RetrievalLogService> _logger;

    public RetrievalLogService(
        KnowledgeRetrievalLogRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor,
        ILogger<RetrievalLogService> logger)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _logger = logger;
    }

    public async Task<PagedResult<RetrievalLogDto>> ListAsync(
        TenantId tenantId,
        long knowledgeBaseId,
        RetrievalLogQuery query,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            tenantId,
            knowledgeBaseId,
            query.FromTs,
            query.ToTs,
            query.PageIndex,
            query.PageSize,
            cancellationToken);

        var dtos = items
            .Select(Map)
            .Where(dto => !query.CallerType.HasValue || dto.CallerContext.CallerType == query.CallerType.Value)
            .ToList();
        return new PagedResult<RetrievalLogDto>(dtos, total, query.PageIndex, query.PageSize);
    }

    public async Task<RetrievalLogDto?> GetAsync(
        TenantId tenantId,
        string traceId,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByTraceIdAsync(tenantId, traceId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task AppendAsync(
        TenantId tenantId,
        RetrievalLogDto log,
        CancellationToken cancellationToken)
    {
        try
        {
            var entity = new KnowledgeRetrievalLogEntity(
                tenantId,
                _idGeneratorAccessor.NextId(),
                log.TraceId,
                log.KnowledgeBaseId,
                log.RawQuery,
                log.RewrittenQuery,
                log.Filters is null ? null : JsonSerializer.Serialize(log.Filters, JsonOptions),
                JsonSerializer.Serialize(log.CallerContext, JsonOptions),
                JsonSerializer.Serialize(log.Candidates, JsonOptions),
                JsonSerializer.Serialize(log.Reranked, JsonOptions),
                log.FinalContext,
                log.EmbeddingModel,
                log.VectorStore,
                log.LatencyMs);
            await _repository.AddAsync(entity, cancellationToken);
        }
        catch (Exception ex)
        {
            // 检索调用日志属于审计辅助，不能让落库失败影响主链路。
            _logger.LogWarning(ex, "Failed to append RetrievalLog {TraceId}", log.TraceId);
        }
    }

    private static RetrievalLogDto Map(KnowledgeRetrievalLogEntity entity)
    {
        IReadOnlyList<RetrievalCandidate> candidates = TryDeserialize<List<RetrievalCandidate>>(entity.CandidatesJson)
            ?? new List<RetrievalCandidate>();
        IReadOnlyList<RetrievalCandidate> reranked = TryDeserialize<List<RetrievalCandidate>>(entity.RerankedJson)
            ?? new List<RetrievalCandidate>();
        var callerContext = TryDeserialize<RetrievalCallerContext>(entity.CallerContextJson)
            ?? new RetrievalCallerContext(KnowledgeRetrievalCallerType.Studio);
        var filters = TryDeserialize<Dictionary<string, string>>(entity.FiltersJson);

        return new RetrievalLogDto(
            entity.TraceId,
            entity.KnowledgeBaseId,
            entity.RawQuery,
            callerContext,
            candidates,
            reranked,
            entity.FinalContext,
            entity.EmbeddingModel,
            entity.VectorStore,
            entity.LatencyMs,
            entity.CreatedAt,
            entity.RewrittenQuery,
            filters);
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
