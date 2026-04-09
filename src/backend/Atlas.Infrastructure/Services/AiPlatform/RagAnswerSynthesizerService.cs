using System.Text.RegularExpressions;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class RagAnswerSynthesizerService : IAnswerSynthesizer
{
    private static readonly Regex CitationRegex = new(@"\[C(?<index>\d+)\]", RegexOptions.Compiled);

    private readonly ILlmProviderFactory _llmProviderFactory;
    private readonly IOptionsMonitor<AiPlatformOptions> _optionsMonitor;

    public RagAnswerSynthesizerService(
        ILlmProviderFactory llmProviderFactory,
        IOptionsMonitor<AiPlatformOptions> optionsMonitor)
    {
        _llmProviderFactory = llmProviderFactory;
        _optionsMonitor = optionsMonitor;
    }

    public async Task<RagAnswerSynthesis> SynthesizeAsync(
        string query,
        IReadOnlyList<RagSearchResult> evidence,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new RagAnswerSynthesis("问题为空。", [], 0f);
        }

        if (evidence.Count == 0)
        {
            return new RagAnswerSynthesis("证据不足，无法生成可靠答案。", [], 0.1f);
        }

        var options = _optionsMonitor.CurrentValue;
        var provider = _llmProviderFactory.GetLlmProvider();
        var model = ResolveModel(options);
        var indexedEvidence = evidence
            .Select((item, index) =>
                $"[C{index + 1}] kb={item.KnowledgeBaseId}, doc={item.DocumentId}, chunk={item.ChunkId}, score={item.Score:F3}\n{item.Content}")
            .ToArray();
        var context = string.Join("\n\n", indexedEvidence);

        var response = await provider.ChatAsync(
            new ChatCompletionRequest(
                Model: model,
                Messages:
                [
                    new ChatMessage(
                        "system",
                        "你是企业级检索问答助手。仅基于提供证据回答，不得编造。答案中每个关键结论都必须带引用编号，如[C1]。如果证据不足请明确说明。"),
                    new ChatMessage(
                        "user",
                        $"问题：{query}\n\n证据：\n{context}\n\n请输出中文答案。")
                ],
                Temperature: 0.2f,
                MaxTokens: 900,
                Provider: "rag.answer_synthesizer"),
            cancellationToken);

        var answer = string.IsNullOrWhiteSpace(response.Content)
            ? "未生成有效答案。"
            : response.Content.Trim();
        var citations = ExtractCitations(answer, evidence);
        var confidence = ComputeConfidence(evidence, citations.Count);
        return new RagAnswerSynthesis(answer, citations, confidence);
    }

    private static IReadOnlyList<RagCitation> ExtractCitations(
        string answer,
        IReadOnlyList<RagSearchResult> evidence)
    {
        if (evidence.Count == 0)
        {
            return [];
        }

        var citationIndexes = CitationRegex.Matches(answer)
            .Select(match => match.Groups["index"].Value)
            .Where(value => int.TryParse(value, out _))
            .Select(int.Parse)
            .Distinct()
            .Where(index => index >= 1 && index <= evidence.Count)
            .ToArray();

        if (citationIndexes.Length == 0)
        {
            var first = evidence[0];
            return
            [
                new RagCitation("C1", first.KnowledgeBaseId, first.DocumentId, first.ChunkId, first.DocumentName)
            ];
        }

        return citationIndexes
            .Select(index =>
            {
                var item = evidence[index - 1];
                return new RagCitation(
                    $"C{index}",
                    item.KnowledgeBaseId,
                    item.DocumentId,
                    item.ChunkId,
                    item.DocumentName);
            })
            .ToArray();
    }

    private static float ComputeConfidence(
        IReadOnlyList<RagSearchResult> evidence,
        int citationCount)
    {
        if (evidence.Count == 0)
        {
            return 0f;
        }

        var avgScore = evidence.Average(item => item.Score);
        var citationFactor = evidence.Count == 0 ? 0f : Math.Clamp(citationCount / (float)evidence.Count, 0f, 1f);
        var confidence = (avgScore * 0.8f) + (citationFactor * 0.2f);
        return Math.Clamp(confidence, 0f, 1f);
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
