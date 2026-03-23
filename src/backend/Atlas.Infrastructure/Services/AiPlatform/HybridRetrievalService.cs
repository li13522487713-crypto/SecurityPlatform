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
    {
        if (topK <= 0)
        {
            return Array.Empty<RagSearchResult>();
        }

        var queryTokens = Tokenize(query);
        var scoreMap = new Dictionary<long, double>();
        var itemMap = new Dictionary<long, RagSearchResult>();
        var rrfK = Math.Max(1, _options.Retrieval.RrfK);

        AccumulateRrf(vectorResults, scoreMap, itemMap, rrfK);
        AccumulateRrf(bm25Results, scoreMap, itemMap, rrfK);

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
        int rrfK)
    {
        for (var i = 0; i < results.Count; i++)
        {
            var item = results[i];
            var rank = i + 1;
            var increment = 1d / (rrfK + rank);

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
