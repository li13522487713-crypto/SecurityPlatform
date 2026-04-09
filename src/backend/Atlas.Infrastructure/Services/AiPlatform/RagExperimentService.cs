using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class RagExperimentService : IRagExperimentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IOptions<AiPlatformOptions> _options;

    public RagExperimentService(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGeneratorAccessor,
        IOptions<AiPlatformOptions> options)
    {
        _db = db;
        _idGeneratorAccessor = idGeneratorAccessor;
        _options = options;
    }

    public Task<RagExperimentDecision> ResolveDecisionAsync(
        TenantId tenantId,
        string query,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var options = _options.Value.RagExperiment;
        if (!options.Enabled)
        {
            return Task.FromResult(new RagExperimentDecision(
                options.ExperimentName,
                "disabled",
                ParseStrategy(options.ControlStrategy, RagRetrieverStrategy.Hybrid),
                false,
                null));
        }

        var normalized = query.Trim();
        var bucket = StableBucket($"{tenantId.Value}:{normalized}");
        var primary = ParseStrategy(options.ControlStrategy, RagRetrieverStrategy.Hybrid);
        var variant = "control";

        if (bucket < Math.Clamp(options.TrafficPercent, 0, 100))
        {
            if (bucket >= Math.Clamp(options.ControlPercent, 0, 100))
            {
                primary = ParseStrategy(options.TreatmentStrategy, RagRetrieverStrategy.Vector);
                variant = "treatment";
            }
        }

        var shadowEnabled = options.ShadowEnabled
            && StableBucket($"shadow:{tenantId.Value}:{normalized}") < Math.Clamp(options.ShadowTrafficPercent, 0, 100);
        RagRetrieverStrategy? shadow = shadowEnabled
            ? ParseStrategy(options.ShadowStrategy, RagRetrieverStrategy.Bm25)
            : null;

        if (shadow == primary)
        {
            shadow = primary == RagRetrieverStrategy.Bm25
                ? RagRetrieverStrategy.Hybrid
                : RagRetrieverStrategy.Bm25;
        }

        return Task.FromResult(new RagExperimentDecision(
            options.ExperimentName,
            variant,
            primary,
            shadowEnabled,
            shadowEnabled ? shadow : null));
    }

    public async Task<long> RecordRunAsync(
        TenantId tenantId,
        RagExperimentRunCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = new RagExperimentRun(
            tenantId,
            _idGeneratorAccessor.NextId(),
            request.ExperimentName,
            request.Variant,
            request.Strategy.ToString(),
            request.QueryHash,
            request.TopK,
            JsonSerializer.Serialize(request.ChunkIds, JsonOptions),
            request.ChunkIds.Count,
            request.LatencyMs,
            request.IsShadow,
            DateTime.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<long> RecordShadowComparisonAsync(
        TenantId tenantId,
        long mainRunId,
        long shadowRunId,
        IReadOnlyList<RagSearchResult> mainResults,
        IReadOnlyList<RagSearchResult> shadowResults,
        CancellationToken cancellationToken = default)
    {
        var mainIds = mainResults.Select(item => item.ChunkId).ToHashSet();
        var shadowIds = shadowResults.Select(item => item.ChunkId).ToHashSet();
        var union = mainIds.Union(shadowIds).Count();
        var overlap = union == 0 ? 0m : Math.Round(mainIds.Intersect(shadowIds).Count() / (decimal)union, 4);
        var mainAvg = mainResults.Count == 0 ? 0m : Math.Round((decimal)mainResults.Average(item => item.Score), 4);
        var shadowAvg = shadowResults.Count == 0 ? 0m : Math.Round((decimal)shadowResults.Average(item => item.Score), 4);

        var mainRun = await _db.Queryable<RagExperimentRun>().FirstAsync(item => item.Id == mainRunId, cancellationToken);
        var shadowRun = await _db.Queryable<RagExperimentRun>().FirstAsync(item => item.Id == shadowRunId, cancellationToken);
        var entity = new RagShadowComparison(
            tenantId,
            _idGeneratorAccessor.NextId(),
            mainRunId,
            shadowRunId,
            mainRun?.ExperimentName ?? "rag_retriever_ab_v1",
            mainRun?.Variant ?? "main",
            shadowRun?.Variant ?? "shadow",
            overlap,
            mainAvg,
            shadowAvg,
            DateTime.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<IReadOnlyList<RagExperimentRunDto>> GetRecentRunsAsync(
        TenantId tenantId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        var take = Math.Clamp(limit, 1, 200);
        var rows = await _db.Queryable<RagExperimentRun>()
            .OrderByDescending(item => item.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new RagExperimentRunDto(
            item.Id,
            item.ExperimentName,
            item.Variant,
            item.Strategy,
            item.QueryHash,
            item.TopK,
            item.HitCount,
            item.LatencyMs,
            item.IsShadow,
            item.CreatedAt)).ToArray();
    }

    public async Task<IReadOnlyList<RagShadowComparisonDto>> GetRecentComparisonsAsync(
        TenantId tenantId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        var take = Math.Clamp(limit, 1, 200);
        var rows = await _db.Queryable<RagShadowComparison>()
            .OrderByDescending(item => item.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
        return rows.Select(item => new RagShadowComparisonDto(
            item.Id,
            item.MainRunId,
            item.ShadowRunId,
            item.ExperimentName,
            item.MainVariant,
            item.ShadowVariant,
            item.OverlapScore,
            item.MainAvgScore,
            item.ShadowAvgScore,
            item.CreatedAt)).ToArray();
    }

    private static int StableBucket(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var value = BitConverter.ToInt32(hash, 0) & int.MaxValue;
        return value % 100;
    }

    private static RagRetrieverStrategy ParseStrategy(string? value, RagRetrieverStrategy fallback)
        => value?.Trim().ToLowerInvariant() switch
        {
            "vector" => RagRetrieverStrategy.Vector,
            "bm25" => RagRetrieverStrategy.Bm25,
            "hybrid" => RagRetrieverStrategy.Hybrid,
            _ => fallback
        };
}
