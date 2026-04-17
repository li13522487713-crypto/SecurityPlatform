using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.Coze;

/// <summary>
/// 评测列表持久化版（M5.1）。复用 EvaluationTask + EvaluationResult 现有 Domain，
/// 通过新加的 EvaluationTask.WorkspaceId 列按工作空间过滤。
///
/// 历史兼容：WorkspaceId 为 null 的旧任务不会出现在任何工作空间的 Coze 列表中，
/// 仅通过 EvaluationService 的老路径访问；不会影响原有评测流程。
/// </summary>
public sealed class WorkspaceEvaluationService : IWorkspaceEvaluationService
{
    private readonly EvaluationTaskRepository _taskRepository;
    private readonly EvaluationResultRepository _resultRepository;

    public WorkspaceEvaluationService(
        EvaluationTaskRepository taskRepository,
        EvaluationResultRepository resultRepository)
    {
        _taskRepository = taskRepository;
        _resultRepository = resultRepository;
    }

    public async Task<PagedResult<EvaluationItemDto>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var pageIndex = Math.Max(1, pagedRequest.PageIndex);
        var pageSize = Math.Clamp(pagedRequest.PageSize, 1, 100);

        var (entities, total) = await _taskRepository.GetPagedByWorkspaceAsync(
            tenantId,
            workspaceId,
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);

        var items = entities.Select(MapToItem).ToArray();
        return new PagedResult<EvaluationItemDto>(items, total, pageIndex, pageSize);
    }

    public async Task<EvaluationDetailDto?> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string evaluationId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(evaluationId, out var id))
        {
            return null;
        }

        var entity = await _taskRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (entity is null || !string.Equals(entity.WorkspaceId, workspaceId, StringComparison.Ordinal))
        {
            return null;
        }

        // EvaluationResult 按 taskId 汇总 Pass/Fail（一次查询）
        var results = await _resultRepository.GetByTaskAsync(tenantId, id, cancellationToken);
        var passCount = results.Count(r => r.Status == EvaluationCaseStatus.Passed);
        var failCount = results.Count(r => r.Status == EvaluationCaseStatus.Failed || r.Status == EvaluationCaseStatus.Error);

        var summary = MapToItem(entity);
        return new EvaluationDetailDto(
            Id: summary.Id,
            Name: summary.Name,
            TargetType: summary.TargetType,
            TargetId: summary.TargetId,
            TestsetId: summary.TestsetId,
            Status: summary.Status,
            MetricSummary: summary.MetricSummary,
            StartedAt: summary.StartedAt,
            TotalCount: entity.TotalCases,
            PassCount: passCount,
            FailCount: failCount,
            ReportJson: entity.AggregateMetricsJson);
    }

    private static EvaluationItemDto MapToItem(EvaluationTask entity)
    {
        var metricSummary = entity.Score == 0
            ? $"{entity.CompletedCases}/{entity.TotalCases}"
            : $"{entity.CompletedCases}/{entity.TotalCases} · score={entity.Score:0.##}";

        return new EvaluationItemDto(
            Id: entity.Id.ToString(),
            Name: entity.Name,
            TargetType: "agent",
            TargetId: entity.AgentId.ToString(),
            TestsetId: entity.DatasetId.ToString(),
            Status: MapStatus(entity.Status),
            MetricSummary: metricSummary,
            StartedAt: entity.StartedAt > DateTime.UnixEpoch
                ? new DateTimeOffset(DateTime.SpecifyKind(entity.StartedAt, DateTimeKind.Utc))
                : new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)));
    }

    private static Atlas.Application.Coze.Models.EvaluationStatus MapStatus(EvaluationTaskStatus status)
    {
        return status switch
        {
            EvaluationTaskStatus.Pending => Atlas.Application.Coze.Models.EvaluationStatus.Pending,
            EvaluationTaskStatus.Running => Atlas.Application.Coze.Models.EvaluationStatus.Running,
            EvaluationTaskStatus.Completed => Atlas.Application.Coze.Models.EvaluationStatus.Succeeded,
            EvaluationTaskStatus.Failed => Atlas.Application.Coze.Models.EvaluationStatus.Failed,
            _ => Atlas.Application.Coze.Models.EvaluationStatus.Pending
        };
    }
}
