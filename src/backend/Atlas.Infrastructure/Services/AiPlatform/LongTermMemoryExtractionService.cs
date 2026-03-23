using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class LongTermMemoryExtractionService : ILongTermMemoryExtractionService
{
    private static readonly string[] MemoryHintKeywords =
    [
        "请记住",
        "记住我",
        "我喜欢",
        "我偏好",
        "我习惯",
        "我叫",
        "我是",
        "我的"
    ];

    private static readonly Regex SentenceSplitRegex = new(@"[。！？!?\n;；]+", RegexOptions.Compiled);
    private static readonly Regex NormalizeRegex = new(@"[\s,，。:：;；!！?？]", RegexOptions.Compiled);
    private static readonly Regex TokenRegex = new(@"[\p{L}\p{Nd}]{2,}", RegexOptions.Compiled);

    private readonly LongTermMemoryRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly MemoryOption _options;

    public LongTermMemoryExtractionService(
        LongTermMemoryRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IOptions<AiPlatformOptions> options)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _options = options.Value.Memory;
    }

    public async Task<IReadOnlyList<LongTermMemoryRecallItem>> RecallAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        string query,
        int? topK,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return [];
        }

        var candidates = await _repository.ListByUserAgentAsync(
            tenantId,
            userId,
            agentId,
            _options.LongTermCandidateCount,
            cancellationToken);
        if (candidates.Count == 0)
        {
            return [];
        }

        var normalizedQuery = NormalizeForKey(query);
        var queryTokens = Tokenize(query);
        var recallTopK = Math.Clamp(topK ?? _options.LongTermRecallTopK, 1, 10);
        var scored = candidates
            .Select(memory => new
            {
                Memory = memory,
                Score = ComputeScore(memory, normalizedQuery, queryTokens)
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Memory.LastReferencedAt)
            .ThenByDescending(x => x.Memory.Id)
            .Take(recallTopK)
            .ToList();

        if (scored.Count == 0)
        {
            return [];
        }

        var touched = scored.Select(x => x.Memory).ToList();
        foreach (var item in touched)
        {
            item.Touch();
        }
        await _repository.UpdateRangeAsync(touched, cancellationToken);

        return scored
            .Select(x => new LongTermMemoryRecallItem(
                x.Memory.Id,
                x.Memory.MemoryKey,
                x.Memory.Content,
                x.Score))
            .ToList();
    }

    public async Task ExtractAsync(
        TenantId tenantId,
        long userId,
        long agentId,
        long conversationId,
        string userMessage,
        string assistantMessage,
        CancellationToken cancellationToken)
    {
        _ = assistantMessage;

        if (!_options.Enabled)
        {
            return;
        }

        var candidates = ExtractCandidates(userMessage);
        if (candidates.Count == 0)
        {
            return;
        }

        var candidateKeys = candidates
            .Select(x => x.MemoryKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existing = await _repository.QueryByKeysAsync(
            tenantId,
            userId,
            agentId,
            candidateKeys,
            cancellationToken);
        var existingMap = existing.ToDictionary(x => x.MemoryKey, x => x, StringComparer.OrdinalIgnoreCase);

        var toInsert = new List<LongTermMemory>();
        var toUpdate = new List<LongTermMemory>();
        foreach (var candidate in candidates)
        {
            if (existingMap.TryGetValue(candidate.MemoryKey, out var memory))
            {
                memory.Reinforce(candidate.Content, candidate.Source, conversationId);
                toUpdate.Add(memory);
                continue;
            }

            toInsert.Add(new LongTermMemory(
                tenantId,
                userId,
                agentId,
                conversationId,
                candidate.MemoryKey,
                candidate.Content,
                candidate.Source,
                _idGeneratorAccessor.NextId()));
        }

        await _repository.AddRangeAsync(toInsert, cancellationToken);
        await _repository.UpdateRangeAsync(toUpdate, cancellationToken);
        await _repository.TrimToMaxCountAsync(
            tenantId,
            userId,
            agentId,
            _options.LongTermMaxRecordsPerUserAgent,
            cancellationToken);
    }

    private static double ComputeScore(LongTermMemory memory, string normalizedQuery, IReadOnlyList<string> queryTokens)
    {
        var score = 0.15 + Math.Min(memory.HitCount, 20) * 0.05;

        var ageHours = Math.Max(1d, (DateTime.UtcNow - memory.LastReferencedAt).TotalHours);
        score += 1d / ageHours;

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var normalizedContent = NormalizeForKey(memory.Content);
            if (normalizedContent.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            {
                score += 2.5;
            }

            foreach (var token in queryTokens)
            {
                if (memory.Content.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                    memory.MemoryKey.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    score += 1.2;
                }
            }
        }

        return score;
    }

    private static List<MemoryCandidate> ExtractCandidates(string userMessage)
    {
        var normalized = userMessage.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        var unique = new Dictionary<string, MemoryCandidate>(StringComparer.OrdinalIgnoreCase);
        var sentences = SentenceSplitRegex
            .Split(normalized)
            .Select(x => x.Trim())
            .Where(x => x.Length >= 4)
            .Take(8)
            .ToList();
        foreach (var sentence in sentences)
        {
            var isHintSentence = MemoryHintKeywords.Any(sentence.Contains);
            if (!isHintSentence)
            {
                continue;
            }

            var compactContent = sentence.Length > 180 ? $"{sentence[..180]}..." : sentence;
            var memoryKey = BuildMemoryKey(compactContent);
            if (string.IsNullOrWhiteSpace(memoryKey))
            {
                continue;
            }

            var source = sentence.Contains("喜欢", StringComparison.OrdinalIgnoreCase) ||
                         sentence.Contains("偏好", StringComparison.OrdinalIgnoreCase) ||
                         sentence.Contains("习惯", StringComparison.OrdinalIgnoreCase)
                ? "preference"
                : "profile";
            unique[memoryKey] = new MemoryCandidate(memoryKey, compactContent, source);
        }

        return unique.Values.ToList();
    }

    private static string BuildMemoryKey(string content)
    {
        var normalized = NormalizeForKey(content);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        return normalized.Length <= 48 ? normalized : normalized[..48];
    }

    private static string NormalizeForKey(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return NormalizeRegex.Replace(text.Trim().ToLowerInvariant(), string.Empty);
    }

    private static IReadOnlyList<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return TokenRegex.Matches(text)
            .Select(match => match.Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Take(12)
            .ToArray();
    }

    private sealed record MemoryCandidate(string MemoryKey, string Content, string Source);
}
