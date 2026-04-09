using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class QueryRewriterService : IQueryRewriter
{
    private readonly ILlmProviderFactory _llmProviderFactory;
    private readonly IOptionsMonitor<AiPlatformOptions> _optionsMonitor;
    private readonly ILogger<QueryRewriterService> _logger;

    public QueryRewriterService(
        ILlmProviderFactory llmProviderFactory,
        IOptionsMonitor<AiPlatformOptions> optionsMonitor,
        ILogger<QueryRewriterService> logger)
    {
        _llmProviderFactory = llmProviderFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> RewriteAsync(
        TenantId tenantId,
        string query,
        int maxQueries = 3,
        CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        var normalized = (query ?? string.Empty).Trim();
        if (normalized.Length == 0 || maxQueries <= 0)
        {
            return [];
        }

        var limitedMaxQueries = Math.Clamp(maxQueries, 1, 8);
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
                            "你是企业知识检索查询改写器。请将用户问题改写为最多 N 条适合检索的查询短句，覆盖定义、条件、步骤、风险等角度。只输出 JSON 数组字符串，例如：[\"...\",\"...\"]。"),
                        new ChatMessage(
                            "user",
                            $"原始问题：{normalized}\n最大条数：{limitedMaxQueries}\n要求：不要输出解释，只输出 JSON 数组。")
                    ],
                    Temperature: 0.1f,
                    MaxTokens: 256,
                    Provider: "rag.query_rewriter"),
                cancellationToken);

            var rewritten = ParseArray(response.Content, limitedMaxQueries);
            if (rewritten.Count == 0)
            {
                return BuildFallback(normalized, limitedMaxQueries);
            }

            if (!rewritten.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                rewritten.Insert(0, normalized);
            }

            return rewritten
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(limitedMaxQueries)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG query rewrite failed, fallback to heuristic rewrite.");
            return BuildFallback(normalized, limitedMaxQueries);
        }
    }

    private static List<string> ParseArray(string? content, int max)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        using var document = JsonDocument.Parse(content);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<string>(max);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = (item.GetString() ?? string.Empty).Trim();
            if (value.Length == 0)
            {
                continue;
            }

            list.Add(value);
            if (list.Count >= max)
            {
                break;
            }
        }

        return list;
    }

    private static IReadOnlyList<string> BuildFallback(string query, int max)
    {
        var list = new List<string>
        {
            query,
            $"{query} 定义",
            $"{query} 步骤",
            $"{query} 风险"
        };

        return list
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(max)
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
