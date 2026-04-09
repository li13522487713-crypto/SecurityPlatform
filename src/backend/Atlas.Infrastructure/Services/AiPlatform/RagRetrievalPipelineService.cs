using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class RagRetrievalPipelineService : IRetrievalPipeline
{
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}_]+", RegexOptions.Compiled);

    private readonly IQueryRewriter _queryRewriter;
    private readonly IRetriever _retriever;
    private readonly IKnowledgeGraphProvider _knowledgeGraphProvider;
    private readonly IPromptGuard _promptGuard;
    private readonly IPiiDetector _piiDetector;
    private readonly IReranker _reranker;
    private readonly IEvidenceScorer _evidenceScorer;
    private readonly IAnswerSynthesizer _answerSynthesizer;
    private readonly IVerificationEngine _verificationEngine;
    private readonly IAtlasHybridCache _cache;
    private readonly ILogger<RagRetrievalPipelineService> _logger;

    public RagRetrievalPipelineService(
        IQueryRewriter queryRewriter,
        IRetriever retriever,
        IKnowledgeGraphProvider knowledgeGraphProvider,
        IPromptGuard promptGuard,
        IPiiDetector piiDetector,
        IReranker reranker,
        IEvidenceScorer evidenceScorer,
        IAnswerSynthesizer answerSynthesizer,
        IVerificationEngine verificationEngine,
        IAtlasHybridCache cache,
        ILogger<RagRetrievalPipelineService> logger)
    {
        _queryRewriter = queryRewriter;
        _retriever = retriever;
        _knowledgeGraphProvider = knowledgeGraphProvider;
        _promptGuard = promptGuard;
        _piiDetector = piiDetector;
        _reranker = reranker;
        _evidenceScorer = evidenceScorer;
        _answerSynthesizer = answerSynthesizer;
        _verificationEngine = verificationEngine;
        _cache = cache;
        _logger = logger;
    }

    public async Task<RagPipelineResult> ExecuteAsync(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        RagPipelineOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveOptions = options ?? new RagPipelineOptions();
        var traces = new List<RagPipelineTrace>();
        var normalizedQuery = (query ?? string.Empty).Trim();
        var cacheKey = BuildCacheKey(tenantId, knowledgeBaseIds, normalizedQuery, effectiveOptions);
        var cacheLookup = await _cache.TryGetAsync<RagPipelineResult>(cacheKey, cancellationToken: cancellationToken);
        if (cacheLookup.Found && cacheLookup.Value is not null)
        {
            return cacheLookup.Value;
        }

        if (normalizedQuery.Length == 0)
        {
            return BuildFailureResult(
                "问题为空。",
                RagFailureMode.EmptyQuery,
                traces,
                []);
        }

        var guardResult = await _promptGuard.CheckAsync(tenantId, normalizedQuery, cancellationToken);
        if (!guardResult.IsSafe)
        {
            traces.Add(Trace("prompt.guard", guardResult.Reason));
            return BuildFailureResult(
                "检测到潜在提示注入风险，请调整输入后重试。",
                RagFailureMode.VerificationFailed,
                traces,
                []);
        }

        traces.Add(Trace("pipeline.start", $"queryLength={normalizedQuery.Length}"));

        IReadOnlyList<string> rewrittenQueries = [normalizedQuery];
        if (effectiveOptions.EnableQueryRewrite)
        {
            rewrittenQueries = await _queryRewriter.RewriteAsync(
                tenantId,
                normalizedQuery,
                maxQueries: 3,
                cancellationToken);
            if (!rewrittenQueries.Contains(normalizedQuery, StringComparer.OrdinalIgnoreCase))
            {
                rewrittenQueries = [normalizedQuery, .. rewrittenQueries];
            }

            traces.Add(Trace("query.rewrite", $"count={rewrittenQueries.Count}"));
        }

        var retrievalCandidates = new List<RagSearchResult>();
        var hopQueries = rewrittenQueries
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var maxHops = 2;
        for (var hop = 0; hop < maxHops && hopQueries.Length > 0; hop++)
        {
            var hopResults = new List<RagSearchResult>();
            foreach (var hopQuery in hopQueries)
            {
                var rows = await _retriever.RetrieveAsync(
                    tenantId,
                    knowledgeBaseIds,
                    hopQuery,
                    effectiveOptions.CandidateTopK,
                    cancellationToken);
                hopResults.AddRange(rows);

                var graphRows = await _knowledgeGraphProvider.SearchAsync(
                    tenantId,
                    knowledgeBaseIds,
                    hopQuery,
                    Math.Max(3, effectiveOptions.TopK),
                    cancellationToken);
                hopResults.AddRange(graphRows.Select(item => item with { Score = (item.Score * 0.92f) + 0.04f }));
            }

            retrievalCandidates.AddRange(hopResults);
            var mergedHop = MergeCandidates(
                retrievalCandidates,
                Math.Max(effectiveOptions.CandidateTopK, effectiveOptions.TopK));
            traces.Add(Trace("retrieval.hop", $"hop={hop + 1}, queries={hopQueries.Length}, merged={mergedHop.Length}"));

            if (mergedHop.Length >= effectiveOptions.TopK || hop == maxHops - 1)
            {
                break;
            }

            hopQueries = BuildFollowUpQueries(normalizedQuery, mergedHop, rewrittenQueries);
            if (hopQueries.Length == 0)
            {
                break;
            }
        }

        var deduped = MergeCandidates(
            retrievalCandidates,
            Math.Max(effectiveOptions.CandidateTopK, effectiveOptions.TopK));
        traces.Add(Trace("retrieval.merge", $"raw={retrievalCandidates.Count}, deduped={deduped.Length}"));

        if (deduped.Length == 0)
        {
            traces.Add(Trace("failure.route", "route=lexical-hint"));
            var lexicalHintRows = await _retriever.RetrieveAsync(
                tenantId,
                knowledgeBaseIds,
                $"{normalizedQuery} 定义 关键点",
                effectiveOptions.CandidateTopK,
                cancellationToken);
            deduped = MergeCandidates(
                lexicalHintRows,
                Math.Max(effectiveOptions.CandidateTopK, effectiveOptions.TopK));
            if (deduped.Length == 0)
            {
                return BuildFailureResult(
                    "未检索到足够证据，请补充关键字后重试。",
                    RagFailureMode.RetrievalEmpty,
                    traces,
                    []);
            }
        }

        IReadOnlyList<RagSearchResult> reranked = deduped;
        if (effectiveOptions.EnableRerank)
        {
            reranked = await _reranker.RerankAsync(
                normalizedQuery,
                deduped,
                Math.Max(effectiveOptions.CandidateTopK, effectiveOptions.TopK),
                cancellationToken);
            traces.Add(Trace("retrieval.rerank", $"count={reranked.Count}"));
        }

        IReadOnlyList<RagSearchResult> selectedEvidence = reranked;
        if (effectiveOptions.EnableEvidenceScoring)
        {
            var scored = new List<(RagSearchResult Item, RagEvidenceScore Score)>(reranked.Count);
            foreach (var evidence in reranked)
            {
                var score = await _evidenceScorer.ScoreAsync(normalizedQuery, evidence, cancellationToken);
                scored.Add((evidence, score));
            }

            selectedEvidence = scored
                .Select(item => new
                {
                    item.Item,
                    Score = (item.Score.Relevance * 0.5f) + (item.Score.Faithfulness * 0.35f) + (item.Score.Freshness * 0.15f)
                })
                .Where(item => item.Score >= effectiveOptions.EvidenceThreshold)
                .OrderByDescending(item => item.Score)
                .Select(item => item.Item)
                .Take(effectiveOptions.TopK)
                .ToArray();
            traces.Add(Trace("evidence.score", $"selected={selectedEvidence.Count}"));

            if (selectedEvidence.Count == 0)
            {
                selectedEvidence = reranked.Take(effectiveOptions.TopK).ToArray();
                traces.Add(Trace("evidence.fallback", "fallback=topk"));
            }
        }
        else
        {
            selectedEvidence = reranked.Take(effectiveOptions.TopK).ToArray();
        }

        RagAnswerSynthesis synthesis;
        try
        {
            synthesis = await _answerSynthesizer.SynthesizeAsync(normalizedQuery, selectedEvidence, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG synthesis failed.");
            return BuildFailureResult(
                "答案生成失败，请稍后重试。",
                RagFailureMode.LlmError,
                traces,
                selectedEvidence);
        }

        traces.Add(Trace("answer.synthesize", $"citationCount={synthesis.Citations.Count}"));
        var piiResult = _piiDetector.Detect(synthesis.Answer);
        if (piiResult.ContainsSensitive)
        {
            traces.Add(Trace("pii.detector", string.Join(",", piiResult.Findings)));
            synthesis = synthesis with { Answer = piiResult.SanitizedText };
        }

        var verification = new RagVerificationResult(
            IsPassed: true,
            RequiresRetry: false,
            SafetyScore: 0.9f,
            Summary: "verification-disabled",
            Issues: []);
        if (effectiveOptions.EnableVerification)
        {
            try
            {
                verification = await _verificationEngine.VerifyAsync(
                    normalizedQuery,
                    synthesis,
                    selectedEvidence,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RAG verification failed.");
                return BuildFailureResult(
                    "答案校验失败，请稍后重试。",
                    RagFailureMode.LlmError,
                    traces,
                    selectedEvidence);
            }

            traces.Add(Trace("answer.verify", $"passed={verification.IsPassed}"));
        }

        if (!verification.IsPassed && verification.RequiresRetry && effectiveOptions.EnableAutoRetry)
        {
            var retries = Math.Max(effectiveOptions.MaxRetries, 0);
            for (var retry = 0; retry < retries && !verification.IsPassed; retry++)
            {
                var retryQuery = $"{normalizedQuery}（请严格基于证据并给出引用）";
                traces.Add(Trace("answer.retry", $"attempt={retry + 1}"));

                synthesis = await _answerSynthesizer.SynthesizeAsync(retryQuery, selectedEvidence, cancellationToken);
                verification = await _verificationEngine.VerifyAsync(
                    normalizedQuery,
                    synthesis,
                    selectedEvidence,
                    cancellationToken);
            }
        }

        if (!verification.IsPassed)
        {
            return new RagPipelineResult(
                synthesis.Answer,
                synthesis.Confidence,
                synthesis.Citations,
                selectedEvidence,
                verification,
                RagFailureMode.VerificationFailed,
                traces);
        }

        var successResult = new RagPipelineResult(
            synthesis.Answer,
            synthesis.Confidence,
            synthesis.Citations,
            selectedEvidence,
            verification,
            RagFailureMode.None,
            traces);
        await _cache.SetAsync(
            cacheKey,
            successResult,
            TimeSpan.FromMinutes(5),
            [$"rag-pipeline:{tenantId.Value:D}"],
            cancellationToken: cancellationToken);
        return successResult;
    }

    private static RagPipelineResult BuildFailureResult(
        string answer,
        RagFailureMode failureMode,
        IReadOnlyList<RagPipelineTrace> traces,
        IReadOnlyList<RagSearchResult> evidence)
    {
        return new RagPipelineResult(
            answer,
            0f,
            [],
            evidence,
            new RagVerificationResult(false, false, 0f, failureMode.ToString(), [failureMode.ToString()]),
            failureMode,
            traces);
    }

    private RagPipelineTrace Trace(string stage, string detail)
    {
        _logger.LogDebug("RAG pipeline stage={Stage}, detail={Detail}", stage, detail);
        return new RagPipelineTrace(stage, detail, DateTimeOffset.UtcNow.ToString("O"));
    }

    private static RagSearchResult[] MergeCandidates(
        IReadOnlyCollection<RagSearchResult> candidates,
        int take)
    {
        if (candidates.Count == 0 || take <= 0)
        {
            return [];
        }

        return candidates
            .GroupBy(item => item.ChunkId)
            .Select(group => group.OrderByDescending(item => item.Score).First())
            .OrderByDescending(item => item.Score)
            .Take(take)
            .ToArray();
    }

    private static string[] BuildFollowUpQueries(
        string baseQuery,
        IReadOnlyList<RagSearchResult> evidences,
        IReadOnlyList<string> seedQueries)
    {
        if (evidences.Count == 0)
        {
            return [];
        }

        var baseTokens = new HashSet<string>(Tokenize(baseQuery), StringComparer.Ordinal);
        var keywordPool = evidences
            .Take(5)
            .SelectMany(item => Tokenize(item.Content))
            .Where(item => item.Length >= 2 && !baseTokens.Contains(item))
            .GroupBy(item => item, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .Take(3)
            .Select(group => group.Key)
            .ToArray();
        if (keywordPool.Length == 0)
        {
            return [];
        }

        var followUps = keywordPool
            .Select(keyword => $"{baseQuery} {keyword} 关联关系")
            .Concat(seedQueries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
        return followUps;
    }

    private static string[] Tokenize(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        return TokenRegex.Matches(content.ToLowerInvariant())
            .Select(item => item.Value)
            .Where(item => item.Length > 1)
            .ToArray();
    }

    private static string BuildCacheKey(
        TenantId tenantId,
        IReadOnlyList<long> knowledgeBaseIds,
        string query,
        RagPipelineOptions options)
    {
        var keySource = JsonSerializer.Serialize(new
        {
            tenantId = tenantId.Value,
            knowledgeBaseIds = knowledgeBaseIds.OrderBy(item => item).ToArray(),
            query = query.Trim(),
            options
        });
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keySource)));
        return $"rag:pipeline:{tenantId.Value:D}:{hash}";
    }
}
