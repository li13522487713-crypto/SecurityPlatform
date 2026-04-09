using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class RagVerificationEngineService : IVerificationEngine
{
    private readonly ILlmProviderFactory _llmProviderFactory;
    private readonly IOptionsMonitor<AiPlatformOptions> _optionsMonitor;
    private readonly ILogger<RagVerificationEngineService> _logger;

    public RagVerificationEngineService(
        ILlmProviderFactory llmProviderFactory,
        IOptionsMonitor<AiPlatformOptions> optionsMonitor,
        ILogger<RagVerificationEngineService> logger)
    {
        _llmProviderFactory = llmProviderFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task<RagVerificationResult> VerifyAsync(
        string query,
        RagAnswerSynthesis answer,
        IReadOnlyList<RagSearchResult> evidence,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new RagVerificationResult(false, false, 0f, "query-empty", ["query-empty"]);
        }

        if (evidence.Count == 0)
        {
            return new RagVerificationResult(false, false, 0.1f, "evidence-empty", ["evidence-empty"]);
        }

        try
        {
            var options = _optionsMonitor.CurrentValue;
            var provider = _llmProviderFactory.GetLlmProvider();
            var model = ResolveModel(options);
            var evidenceContext = string.Join(
                "\n\n",
                evidence.Select((item, index) =>
                    $"[C{index + 1}] doc={item.DocumentId}, chunk={item.ChunkId}, score={item.Score:F3}\n{item.Content}"));
            var response = await provider.ChatAsync(
                new ChatCompletionRequest(
                    Model: model,
                    Messages:
                    [
                        new ChatMessage(
                            "system",
                            "你是答案验证器。请仅输出 JSON：{isPassed:boolean,requiresRetry:boolean,safetyScore:number,summary:string,issues:string[]}。"),
                        new ChatMessage(
                            "user",
                            $"问题：{query}\n\n答案：{answer.Answer}\n\n引用：{string.Join(",", answer.Citations.Select(item => item.Label))}\n\n证据：\n{evidenceContext}\n\n请验证答案是否忠于证据并且安全。")
                    ],
                    Temperature: 0.1f,
                    MaxTokens: 300,
                    Provider: "rag.verification_engine"),
                cancellationToken);

            var parsed = Parse(response.Content);
            if (parsed is not null)
            {
                return parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG verification failed, fallback to heuristic result.");
        }

        var fallbackPassed = answer.Citations.Count > 0;
        return new RagVerificationResult(
            fallbackPassed,
            !fallbackPassed,
            fallbackPassed ? 0.7f : 0.2f,
            fallbackPassed ? "fallback-passed" : "fallback-retry",
            fallbackPassed ? [] : ["citation-missing"]);
    }

    private static RagVerificationResult? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        using var document = JsonDocument.Parse(content);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var root = document.RootElement;
        var isPassed = ReadBool(root, "isPassed");
        var requiresRetry = ReadBool(root, "requiresRetry");
        var safetyScore = ReadFloat(root, "safetyScore");
        var summary = root.TryGetProperty("summary", out var summaryElement)
            ? (summaryElement.GetString() ?? "verified")
            : "verified";
        var issues = root.TryGetProperty("issues", out var issuesElement) && issuesElement.ValueKind == JsonValueKind.Array
            ? issuesElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => item.Length > 0)
                .ToArray()
            : [];

        return new RagVerificationResult(
            isPassed,
            requiresRetry,
            Math.Clamp(safetyScore, 0f, 1f),
            summary,
            issues);
    }

    private static bool ReadBool(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => false
        };
    }

    private static float ReadFloat(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return 0f;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetSingle(out var parsed) => parsed,
            JsonValueKind.String when float.TryParse(value.GetString(), out var parsed) => parsed,
            _ => 0f
        };
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
