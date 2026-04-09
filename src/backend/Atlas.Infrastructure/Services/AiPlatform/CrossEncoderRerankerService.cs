using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class CrossEncoderRerankerService
{
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}_]+", RegexOptions.Compiled);

    public IReadOnlyList<RagSearchResult> Rerank(
        string query,
        IReadOnlyList<RagSearchResult> candidates,
        int topK)
    {
        if (string.IsNullOrWhiteSpace(query) || candidates.Count == 0 || topK <= 0)
        {
            return candidates.Take(Math.Max(topK, 0)).ToArray();
        }

        var queryTokens = Tokenize(query);
        if (queryTokens.Length == 0)
        {
            return candidates.Take(topK).ToArray();
        }

        return candidates
            .Select(candidate =>
            {
                var lexical = ComputeLexicalMatch(queryTokens, candidate.Content);
                var semantic = Math.Max(0f, candidate.Score);
                var crossScore = (semantic * 0.7f) + (lexical * 0.3f);
                return candidate with { Score = crossScore };
            })
            .OrderByDescending(item => item.Score)
            .Take(topK)
            .ToArray();
    }

    private static float ComputeLexicalMatch(IReadOnlyCollection<string> queryTokens, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0f;
        }

        var contentTokens = Tokenize(content);
        if (contentTokens.Length == 0)
        {
            return 0f;
        }

        var contentSet = new HashSet<string>(contentTokens, StringComparer.Ordinal);
        var overlap = queryTokens.Count(contentSet.Contains);
        if (overlap <= 0)
        {
            return 0f;
        }

        return overlap / (float)queryTokens.Count;
    }

    private static string[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return TokenRegex.Matches(text.ToLowerInvariant())
            .Select(item => item.Value)
            .Where(item => item.Length > 1)
            .ToArray();
    }
}
