using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class HybridRetrievalService
{
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}_]+", RegexOptions.Compiled);
    private readonly AiPlatformOptions _options;

    public HybridRetrievalService(IOptions<AiPlatformOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<RagSearchResult> MergeAndRerank(
        string query,
        IReadOnlyList<RagSearchResult> vectorResults,
        IReadOnlyList<RagSearchResult> bm25Results,
        int topK)
        => MergeAndRerankWithWeights(query, vectorResults, bm25Results, topK, vectorWeight: 1d, bm25Weight: 1d);

    /// <summary>
    /// v5 §38 / 计划 G4：加权 RRF 合并。<paramref name="vectorWeight"/> / <paramref name="bm25Weight"/>
    /// 来自调用方 RetrievalProfile.Weights，1.0 表示等权（与传统 RRF 等价）。
    /// </summary>
    public IReadOnlyList<RagSearchResult> MergeAndRerankWithWeights(
        string query,
        IReadOnlyList<RagSearchResult> vectorResults,
        IReadOnlyList<RagSearchResult> bm25Results,
        int topK,
        double vectorWeight,
        double bm25Weight)
    {
        if (topK <= 0)
        {
            return Array.Empty<RagSearchResult>();
        }

        var queryTokens = Tokenize(query);
        var scoreMap = new Dictionary<long, double>();
        var itemMap = new Dictionary<long, RagSearchResult>();
        var rrfK = Math.Max(1, _options.Retrieval.RrfK);

        AccumulateRrf(vectorResults, scoreMap, itemMap, rrfK, vectorWeight);
        AccumulateRrf(bm25Results, scoreMap, itemMap, rrfK, bm25Weight);

        if (_options.Retrieval.EnableRerank && queryTokens.Length > 0)
        {
            foreach (var chunkId in scoreMap.Keys.ToArray())
            {
                if (!itemMap.TryGetValue(chunkId, out var item))
                {
                    continue;
                }

                scoreMap[chunkId] += 0.1d * OverlapScore(queryTokens, item.Content);
            }
        }

        return scoreMap
            .OrderByDescending(pair => pair.Value)
            .Take(topK)
            .Where(pair => itemMap.ContainsKey(pair.Key))
            .Select(pair =>
            {
                var item = itemMap[pair.Key];
                return item with { Score = (float)pair.Value };
            })
            .ToArray();
    }

    private static void AccumulateRrf(
        IReadOnlyList<RagSearchResult> results,
        IDictionary<long, double> scoreMap,
        IDictionary<long, RagSearchResult> itemMap,
        int rrfK,
        double weight = 1d)
    {
        if (weight <= 0d) return;
        for (var i = 0; i < results.Count; i++)
        {
            var item = results[i];
            var rank = i + 1;
            var increment = weight * (1d / (rrfK + rank));

            scoreMap[item.ChunkId] = scoreMap.TryGetValue(item.ChunkId, out var currentScore)
                ? currentScore + increment
                : increment;
            if (!itemMap.ContainsKey(item.ChunkId))
            {
                itemMap[item.ChunkId] = item;
            }
        }
    }

    private static float OverlapScore(IReadOnlyCollection<string> queryTokens, string text)
    {
        var textTokens = Tokenize(text);
        if (textTokens.Length == 0)
        {
            return 0;
        }

        var overlap = textTokens.Count(token => queryTokens.Contains(token, StringComparer.Ordinal));
        if (overlap == 0)
        {
            return 0;
        }

        return overlap / (float)queryTokens.Count;
    }

    private static string[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return TokenRegex.Matches(text.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(token => token.Length > 1)
            .ToArray();
    }
}
