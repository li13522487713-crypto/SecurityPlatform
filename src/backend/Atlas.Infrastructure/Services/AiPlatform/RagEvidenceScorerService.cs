using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class RagEvidenceScorerService : IEvidenceScorer
{
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{N}_]+", RegexOptions.Compiled);

    private readonly ILlmProviderFactory _llmProviderFactory;
    private readonly IOptionsMonitor<AiPlatformOptions> _optionsMonitor;
    private readonly ILogger<RagEvidenceScorerService> _logger;

    public RagEvidenceScorerService(
        ILlmProviderFactory llmProviderFactory,
        IOptionsMonitor<AiPlatformOptions> optionsMonitor,
        ILogger<RagEvidenceScorerService> logger)
    {
        _llmProviderFactory = llmProviderFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<RagEvidenceScore> ScoreAsync(
        string query,
        RagSearchResult evidence,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(evidence.Content))
        {
            return new RagEvidenceScore(0f, 0f, 0f, "empty-input");
        }

        try
        {
            var options = _optionsMonitor.CurrentValue;
            var provider = _llmProviderFactory.GetLlmProvider();
            var model = ResolveModel(options);
            var response = await provider.ChatAsync(
                new ChatCompletionRequest(
                    Model: model,
                    Messages:
                    [
                        new ChatMessage(
                            "system",
                            "你是证据评分器。请只输出 JSON，字段：relevance, faithfulness, freshness, summary。分值范围 0-1。"),
                        new ChatMessage(
                            "user",
                            $"Query: {query}\nEvidence: {evidence.Content}\nDocumentCreatedAt(UTC): {evidence.DocumentCreatedAt:O}\n请返回 JSON。")
                    ],
                    Temperature: 0.1f,
                    MaxTokens: 200,
                    Provider: "rag.evidence_scorer"),
                cancellationToken);

            var parsed = Parse(response.Content);
            if (parsed is not null)
            {
                return parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG evidence scoring failed, fallback to heuristic score.");
        }

        return BuildFallbackScore(query, evidence);
    }

    private static RagEvidenceScore? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(content);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var root = doc.RootElement;
        var relevance = ReadScore(root, "relevance");
        var faithfulness = ReadScore(root, "faithfulness");
        var freshness = ReadScore(root, "freshness");
        var summary = root.TryGetProperty("summary", out var summaryElement)
            ? summaryElement.GetString()
            : null;
        return new RagEvidenceScore(relevance, faithfulness, freshness, summary);
    }

    private static float ReadScore(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return 0f;
        }

        var score = property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetSingle(out var single) => single,
            JsonValueKind.String when float.TryParse(property.GetString(), out var parsed) => parsed,
            _ => 0f
        };

        return Math.Clamp(score, 0f, 1f);
    }

    private static RagEvidenceScore BuildFallbackScore(string query, RagSearchResult evidence)
    {
        var queryTokens = Tokenize(query);
        var contentTokens = Tokenize(evidence.Content);
        var overlap = queryTokens.Length == 0
            ? 0f
            : contentTokens.Count(item => queryTokens.Contains(item, StringComparer.Ordinal)) / (float)queryTokens.Length;
        var relevance = Math.Clamp(overlap, 0f, 1f);
        var faithfulness = Math.Clamp(evidence.Score, 0f, 1f);
        var freshness = 0.4f;
        if (evidence.DocumentCreatedAt.HasValue)
        {
            var days = Math.Max(0d, (DateTime.UtcNow - evidence.DocumentCreatedAt.Value).TotalDays);
            freshness = (float)Math.Exp(-Math.Log(2d) * (days / 30d));
        }

        return new RagEvidenceScore(
            relevance,
            faithfulness,
            Math.Clamp(freshness, 0f, 1f),
            "fallback-heuristic");
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

    private static string ResolveModel(AiPlatformOptions options)
    {
        if (options.Providers.TryGetValue(options.DefaultProvider, out var provider) &&
            !string.IsNullOrWhiteSpace(provider.DefaultModel))
        {
            return provider.DefaultModel;
        }

        return "gpt-4o-mini";
    }
}
