using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class ContextCompressionService
{
    private static readonly char[] SentenceSeparators = ['。', '！', '？', '.', '!', '?', '\n'];
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}_]+", RegexOptions.Compiled);

    public IReadOnlyList<RagSearchResult> Compress(
        string query,
        IReadOnlyList<RagSearchResult> results,
        int maxChars)
    {
        if (results.Count == 0 || maxChars <= 0 || string.IsNullOrWhiteSpace(query))
        {
            return results;
        }

        var queryTokens = Tokenize(query);
        if (queryTokens.Length == 0)
        {
            return results.Select(item => TrimResult(item, maxChars)).ToArray();
        }

        return results.Select(item => CompressSingle(item, queryTokens, maxChars)).ToArray();
    }

    private static RagSearchResult CompressSingle(
        RagSearchResult result,
        IReadOnlyCollection<string> queryTokens,
        int maxChars)
    {
        if (string.IsNullOrWhiteSpace(result.Content) || result.Content.Length <= maxChars)
        {
            return result;
        }

        var segments = SplitToSentences(result.Content);
        var ranked = segments
            .Select((segment, index) => new
            {
                Index = index,
                Segment = segment,
                Score = SegmentScore(segment, queryTokens)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Index)
            .ToList();

        if (ranked.Count == 0)
        {
            return TrimResult(result, maxChars);
        }

        var selected = new List<string>();
        var currentLength = 0;
        foreach (var item in ranked)
        {
            if (item.Score <= 0f && selected.Count > 0)
            {
                continue;
            }

            var candidate = item.Segment.Trim();
            if (candidate.Length == 0)
            {
                continue;
            }

            if (currentLength + candidate.Length > maxChars)
            {
                if (selected.Count == 0)
                {
                    selected.Add(candidate[..Math.Min(maxChars, candidate.Length)]);
                }

                break;
            }

            selected.Add(candidate);
            currentLength += candidate.Length + 1;
            if (currentLength >= maxChars)
            {
                break;
            }
        }

        if (selected.Count == 0)
        {
            return TrimResult(result, maxChars);
        }

        var compressed = string.Join(" ", selected);
        if (compressed.Length > maxChars)
        {
            compressed = compressed[..maxChars];
        }

        return result with { Content = compressed };
    }

    private static RagSearchResult TrimResult(RagSearchResult result, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(result.Content) || result.Content.Length <= maxChars)
        {
            return result;
        }

        return result with { Content = result.Content[..maxChars] };
    }

    private static float SegmentScore(string segment, IReadOnlyCollection<string> queryTokens)
    {
        if (segment.Length == 0)
        {
            return 0f;
        }

        var tokens = Tokenize(segment);
        if (tokens.Length == 0)
        {
            return 0f;
        }

        var overlap = tokens.Count(token => queryTokens.Contains(token, StringComparer.Ordinal));
        if (overlap <= 0)
        {
            return 0f;
        }

        return overlap / (float)queryTokens.Count;
    }

    private static IReadOnlyList<string> SplitToSentences(string text)
    {
        var parts = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (!SentenceSeparators.Contains(text[i]))
            {
                continue;
            }

            var segment = text[start..(i + 1)];
            if (!string.IsNullOrWhiteSpace(segment))
            {
                parts.Add(segment);
            }

            start = i + 1;
        }

        if (start < text.Length)
        {
            var tail = text[start..];
            if (!string.IsNullOrWhiteSpace(tail))
            {
                parts.Add(tail);
            }
        }

        return parts;
    }

    private static string[] Tokenize(string text)
    {
        return TokenRegex.Matches(text.ToLowerInvariant())
            .Select(item => item.Value)
            .Where(item => item.Length > 1)
            .ToArray();
    }
}
