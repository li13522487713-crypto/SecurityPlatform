using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class EvaluationJobService : IEvaluationJobService
{
    private static readonly Regex TokenSplitter = new("[\\s,.;:!?，。；：！？]+", RegexOptions.Compiled);

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
            task.MarkCompleted(completedCases, average);
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
                new AgentChatRequest(null, evaluationCase.Input, false),
                CancellationToken.None);
            var score = ScoreResponse(evaluationCase.ExpectedOutput, response.Content, out var reason);
            var status = score >= 0.7m ? EvaluationCaseStatus.Passed : EvaluationCaseStatus.Failed;
            return new EvaluationResult(
                tenantId,
                task.Id,
                evaluationCase.Id,
                response.Content,
                score,
                reason,
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
