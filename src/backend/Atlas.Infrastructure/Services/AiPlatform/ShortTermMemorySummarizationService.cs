using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class ShortTermMemorySummarizationService : IShortTermMemorySummarizationService
{
    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly ShortTermMemoryRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly MemoryOption _options;

    public ShortTermMemorySummarizationService(
        ShortTermMemoryRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IOptions<AiPlatformOptions> options)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _options = options.Value.Memory;
    }

    public async Task<string?> GetSummaryAsync(
        TenantId tenantId,
        long conversationId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        var memory = await _repository.FindByConversationAsync(tenantId, conversationId, cancellationToken);
        return memory?.Summary;
    }

    public async Task<string?> TrySummarizeAsync(
        TenantId tenantId,
        long conversationId,
        long agentId,
        long userId,
        IReadOnlyList<ChatHistoryMessage> messages,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        var usableMessages = messages
            .Where(x =>
                !x.IsContextCleared &&
                (string.Equals(x.Role, "user", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(x.Role, "assistant", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(x.Content))
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.MessageId)
            .ToList();

        if (usableMessages.Count < _options.ShortTermTriggerMessageCount)
        {
            return null;
        }

        var reserveRecent = Math.Max(1, _options.ShortTermReserveRecentMessages);
        var cutoffCount = usableMessages.Count - reserveRecent;
        if (cutoffCount <= 0)
        {
            return null;
        }

        var existing = await _repository.FindByConversationAsync(tenantId, conversationId, cancellationToken);
        var summarizedCount = existing?.SummarizedMessageCount ?? 0;
        if (cutoffCount <= summarizedCount)
        {
            return existing?.Summary;
        }

        var deltaCount = cutoffCount - summarizedCount;
        if (deltaCount < _options.ShortTermMinIncrementalMessages && existing is not null)
        {
            return existing.Summary;
        }

        var incrementalMessages = usableMessages
            .Skip(summarizedCount)
            .Take(deltaCount)
            .ToList();
        var incrementalSummary = RenderSummary(incrementalMessages);
        if (string.IsNullOrWhiteSpace(incrementalSummary))
        {
            return existing?.Summary;
        }

        var mergedSummary = MergeSummary(existing?.Summary, incrementalSummary);
        if (string.IsNullOrWhiteSpace(mergedSummary))
        {
            return existing?.Summary;
        }

        if (existing is null)
        {
            var created = new ShortTermMemory(
                tenantId,
                conversationId,
                agentId,
                userId,
                mergedSummary,
                cutoffCount,
                _idGeneratorAccessor.NextId());
            await _repository.AddAsync(created, cancellationToken);
        }
        else
        {
            existing.UpdateSummary(mergedSummary, cutoffCount);
            await _repository.UpdateAsync(existing, cancellationToken);
        }

        return mergedSummary;
    }

    private string RenderSummary(IReadOnlyList<ChatHistoryMessage> messages)
    {
        var builder = new StringBuilder();
        foreach (var message in messages)
        {
            var role = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                ? "助手"
                : "用户";
            var compactContent = MultiWhitespaceRegex.Replace(message.Content.Trim(), " ");
            if (compactContent.Length > 160)
            {
                compactContent = $"{compactContent[..160]}...";
            }

            builder.Append(role)
                .Append("：")
                .AppendLine(compactContent);
        }

        return builder.ToString().Trim();
    }

    private string MergeSummary(string? existingSummary, string incrementalSummary)
    {
        var merged = string.IsNullOrWhiteSpace(existingSummary)
            ? incrementalSummary
            : $"{existingSummary}\n{incrementalSummary}";
        if (merged.Length <= _options.ShortTermMaxSummaryLength)
        {
            return merged;
        }

        return merged[^_options.ShortTermMaxSummaryLength..];
    }
}
