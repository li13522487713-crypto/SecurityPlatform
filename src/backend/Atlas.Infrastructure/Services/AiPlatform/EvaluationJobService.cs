using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class EvaluationJobService : IEvaluationJobService
{
    private static readonly Regex TokenSplitter = new("[\\s,.;:!?，。；：！？]+", RegexOptions.Compiled);
    private static readonly Regex CitationRegex = new(@"\[C(?<index>\d+)\]", RegexOptions.Compiled);

    private readonly EvaluationTaskRepository _taskRepository;
    private readonly EvaluationCaseRepository _caseRepository;
    private readonly EvaluationResultRepository _resultRepository;
    private readonly IAgentChatService _agentChatService;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly ILogger<EvaluationJobService> _logger;

    public EvaluationJobService(
        EvaluationTaskRepository taskRepository,
        EvaluationCaseRepository caseRepository,
        EvaluationResultRepository resultRepository,
        IAgentChatService agentChatService,
        IIdGeneratorAccessor idGeneratorAccessor,
        ILogger<EvaluationJobService> logger)
    {
        _taskRepository = taskRepository;
        _caseRepository = caseRepository;
        _resultRepository = resultRepository;
        _agentChatService = agentChatService;
        _idGeneratorAccessor = idGeneratorAccessor;
        _logger = logger;
    }

    public async Task ExecuteTaskAsync(long taskId)
    {
        var task = await _taskRepository.FindByIdAnyTenantAsync(taskId);
        if (task is null)
        {
            return;
        }

        var tenantId = new TenantId(task.TenantIdValue);
        var cases = await _caseRepository.GetByDatasetAsync(tenantId, task.DatasetId, CancellationToken.None);
        task.MarkRunning(cases.Count);
        await _taskRepository.UpdateAsync(task, CancellationToken.None);

        var results = new List<EvaluationResult>(cases.Count);
        var scoreSum = 0m;
        var completedCases = 0;

        try
        {
            foreach (var evaluationCase in cases)
            {
                var result = await ExecuteCaseAsync(tenantId, task, evaluationCase);
                results.Add(result);
                scoreSum += result.Score;
                completedCases++;
            }

            await _resultRepository.AddRangeAsync(results, CancellationToken.None);
            var average = completedCases == 0 ? 0 : Math.Round(scoreSum / completedCases, 4);
            var aggregateMetrics = BuildAggregateMetrics(results);
            task.MarkCompleted(completedCases, average, JsonSerializer.Serialize(aggregateMetrics));
            await _taskRepository.UpdateAsync(task, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "evaluation task failed, taskId={TaskId}", taskId);
            if (results.Count > 0)
            {
                await _resultRepository.AddRangeAsync(results, CancellationToken.None);
            }

            task.MarkFailed(ex.Message, completedCases);
            await _taskRepository.UpdateAsync(task, CancellationToken.None);
        }
    }

    private async Task<EvaluationResult> ExecuteCaseAsync(
        TenantId tenantId,
        EvaluationTask task,
        EvaluationCase evaluationCase)
    {
        try
        {
            var response = await _agentChatService.ChatAsync(
                tenantId,
                task.CreatedByUserId,
                task.AgentId,
                new AgentChatRequest(null, evaluationCase.Input, task.EnableRag),
                CancellationToken.None);
            var score = ScoreResponse(evaluationCase.ExpectedOutput, response.Content, out var reason);
            var metrics = BuildRagMetrics(evaluationCase, response.Content, score);
            var metricsJson = JsonSerializer.Serialize(metrics);
            var status = score >= 0.7m ? EvaluationCaseStatus.Passed : EvaluationCaseStatus.Failed;
            var faithfulness = metrics.GetValueOrDefault("faithfulness");
            var contextPrecision = metrics.GetValueOrDefault("contextPrecision");
            var contextRecall = metrics.GetValueOrDefault("contextRecall");
            var answerRelevance = metrics.GetValueOrDefault("answerRelevance");
            var citationAccuracy = metrics.GetValueOrDefault("citationAccuracy");
            var hallucinationRate = metrics.GetValueOrDefault("hallucinationRate");
            return new EvaluationResult(
                tenantId,
                task.Id,
                evaluationCase.Id,
                response.Content,
                score,
                $"{reason} | Faithfulness={faithfulness:F2}, Citation={citationAccuracy:F2}",
                faithfulness,
                contextPrecision,
                contextRecall,
                answerRelevance,
                citationAccuracy,
                hallucinationRate,
                metricsJson,
                status,
                NextIdForBackground());
        }
        catch (Exception ex)
        {
            return new EvaluationResult(
                tenantId,
                task.Id,
                evaluationCase.Id,
                actualOutput: string.Empty,
                score: 0,
                judgeReason: $"执行失败：{ex.Message}",
                faithfulnessScore: 0,
                contextPrecisionScore: 0,
                contextRecallScore: 0,
                answerRelevanceScore: 0,
                citationAccuracyScore: 0,
                hallucinationScore: 1,
                ragMetricsJson: "{}",
                status: EvaluationCaseStatus.Error,
                id: NextIdForBackground());
        }
    }

    private static decimal ScoreResponse(string expected, string actual, out string reason)
    {
        var normalizedExpected = expected?.Trim() ?? string.Empty;
        var normalizedActual = actual?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedExpected))
        {
            if (string.IsNullOrWhiteSpace(normalizedActual))
            {
                reason = "期望为空且输出为空，得分0。";
                return 0;
            }

            reason = "无标准答案，按非空输出记为0.8。";
            return 0.8m;
        }

        if (normalizedActual.Contains(normalizedExpected, StringComparison.OrdinalIgnoreCase))
        {
            reason = "输出包含期望答案关键词。";
            return 1m;
        }

        var expectedTokens = Tokenize(normalizedExpected);
        var actualTokens = Tokenize(normalizedActual);
        if (expectedTokens.Count == 0 || actualTokens.Count == 0)
        {
            reason = "无法分词或输出为空。";
            return 0;
        }

        var intersection = expectedTokens.Intersect(actualTokens, StringComparer.OrdinalIgnoreCase).Count();
        var ratio = (decimal)intersection / expectedTokens.Count;
        reason = $"关键词命中率：{ratio:P1}";
        return Math.Round(ratio, 4);
    }

    private static HashSet<string> Tokenize(string text)
    {
        return TokenSplitter
            .Split(text)
            .Select(token => token.Trim())
            .Where(token => token.Length >= 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, decimal> BuildRagMetrics(
        EvaluationCase evaluationCase,
        string actualOutput,
        decimal lexicalScore)
    {
        var expectedCitations = ParseStringArray(evaluationCase.GroundTruthCitationsJson);
        var actualCitations = CitationRegex.Matches(actualOutput ?? string.Empty)
            .Select(match => $"C{match.Groups["index"].Value}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var citationAccuracy = expectedCitations.Count == 0
            ? (actualCitations.Count > 0 ? 0.8m : 0.4m)
            : Math.Round((decimal)actualCitations.Intersect(expectedCitations, StringComparer.OrdinalIgnoreCase).Count() / expectedCitations.Count, 4);
        var contextPrecision = actualCitations.Count == 0
            ? 0
            : Math.Round((decimal)actualCitations.Intersect(expectedCitations, StringComparer.OrdinalIgnoreCase).Count() / actualCitations.Count, 4);
        var contextRecall = expectedCitations.Count == 0
            ? (actualCitations.Count > 0 ? 1m : 0m)
            : Math.Round((decimal)actualCitations.Intersect(expectedCitations, StringComparer.OrdinalIgnoreCase).Count() / expectedCitations.Count, 4);
        var faithfulness = Math.Clamp(lexicalScore, 0m, 1m);
        var answerRelevance = Math.Clamp(lexicalScore, 0m, 1m);
        var hallucinationRate = Math.Clamp(1m - faithfulness, 0m, 1m);

        return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["faithfulness"] = faithfulness,
            ["contextPrecision"] = contextPrecision,
            ["contextRecall"] = contextRecall,
            ["answerRelevance"] = answerRelevance,
            ["citationAccuracy"] = citationAccuracy,
            ["hallucinationRate"] = hallucinationRate
        };
    }

    private static IReadOnlyList<string> ParseStringArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Dictionary<string, decimal> BuildAggregateMetrics(IReadOnlyList<EvaluationResult> results)
    {
        if (results.Count == 0)
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        decimal Average(Func<EvaluationResult, decimal> selector)
            => Math.Round(results.Average(selector), 4);

        return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["faithfulness"] = Average(item => item.FaithfulnessScore),
            ["contextPrecision"] = Average(item => item.ContextPrecisionScore),
            ["contextRecall"] = Average(item => item.ContextRecallScore),
            ["answerRelevance"] = Average(item => item.AnswerRelevanceScore),
            ["citationAccuracy"] = Average(item => item.CitationAccuracyScore),
            ["hallucinationRate"] = Average(item => item.HallucinationScore)
        };
    }

    private long NextIdForBackground()
    {
        try
        {
            return _idGeneratorAccessor.NextId();
        }
        catch
        {
            var high = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
            var low = Random.Shared.Next(1, 999);
            return high + low;
        }
    }
}
