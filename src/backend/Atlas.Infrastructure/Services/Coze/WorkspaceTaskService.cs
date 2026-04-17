using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.Coze;

/// <summary>
/// 任务中心持久化版（M4.4）。
///
/// 当前数据源仅 <see cref="EvaluationTask"/>（评测任务，按 tenantId 过滤）。
/// 后续接入 BatchJobExecution / PersistedExecutionPointer / Hangfire job 时再加聚合源。
///
/// 限制：现有 EvaluationTask 模型无 workspaceId 字段，租户内任务对所有工作空间可见；
/// 第二轮 schema 演进时为 EvaluationTask 增加 WorkspaceId 列后即可严格按工作空间过滤。
/// </summary>
public sealed class WorkspaceTaskService : IWorkspaceTaskService
{
    private const string TaskType = "evaluation";

    private readonly EvaluationTaskRepository _evaluationTaskRepository;

    public WorkspaceTaskService(EvaluationTaskRepository evaluationTaskRepository)
    {
        _evaluationTaskRepository = evaluationTaskRepository;
    }

    public async Task<PagedResult<WorkspaceTaskItemDto>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        WorkspaceTaskStatus? status,
        string? type,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var pageIndex = Math.Max(1, pagedRequest.PageIndex);
        var pageSize = Math.Clamp(pagedRequest.PageSize, 1, 100);

        // 仅当 type 未指定或匹配 "evaluation" 时返回数据；其它 type 暂无对应数据源。
        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, TaskType, StringComparison.OrdinalIgnoreCase))
        {
            return new PagedResult<WorkspaceTaskItemDto>(Array.Empty<WorkspaceTaskItemDto>(), 0, pageIndex, pageSize);
        }

        var (entities, total) = await _evaluationTaskRepository.GetPagedAsync(tenantId, pageIndex, pageSize, cancellationToken);

        var items = entities
            .Select(MapItem)
            .Where(item => MatchStatus(item.Status, status) && MatchKeyword(item.Name, keyword))
            .ToArray();

        // 这里的 total 仍取自数据库总数，前端按此分页；状态/关键字过滤是 best-effort，
        // 第二轮把 status / keyword 下推到 SQL 层后再返回严格分页。
        return new PagedResult<WorkspaceTaskItemDto>(items, total, pageIndex, pageSize);
    }

    public async Task<WorkspaceTaskDetailDto?> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string taskId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(taskId, out var id))
        {
            return null;
        }

        var entity = await _evaluationTaskRepository.FindByIdAsync(tenantId, id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var summary = MapItem(entity);
        return new WorkspaceTaskDetailDto(
            Id: summary.Id,
            Name: summary.Name,
            Type: summary.Type,
            Status: summary.Status,
            StartedAt: summary.StartedAt,
            DurationMs: summary.DurationMs,
            OwnerDisplayName: summary.OwnerDisplayName,
            InputJson: null,
            OutputJson: entity.AggregateMetricsJson,
            ErrorMessage: string.IsNullOrWhiteSpace(entity.ErrorMessage) ? null : entity.ErrorMessage,
            Logs: Array.Empty<WorkspaceTaskLogEntryDto>());
    }

    private static WorkspaceTaskItemDto MapItem(EvaluationTask entity)
    {
        var startedAt = entity.StartedAt > DateTime.UnixEpoch
            ? new DateTimeOffset(DateTime.SpecifyKind(entity.StartedAt, DateTimeKind.Utc))
            : new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc));

        var durationMs = entity.CompletedAt > DateTime.UnixEpoch && entity.StartedAt > DateTime.UnixEpoch
            ? (long)(entity.CompletedAt - entity.StartedAt).TotalMilliseconds
            : 0L;

        return new WorkspaceTaskItemDto(
            Id: entity.Id.ToString(),
            Name: entity.Name,
            Type: TaskType,
            Status: MapStatus(entity.Status),
            StartedAt: startedAt,
            DurationMs: Math.Max(0, durationMs),
            OwnerDisplayName: entity.CreatedByUserId.ToString());
    }

    private static WorkspaceTaskStatus MapStatus(EvaluationTaskStatus status)
    {
        return status switch
        {
            EvaluationTaskStatus.Pending => WorkspaceTaskStatus.Pending,
            EvaluationTaskStatus.Running => WorkspaceTaskStatus.Running,
            EvaluationTaskStatus.Completed => WorkspaceTaskStatus.Succeeded,
            EvaluationTaskStatus.Failed => WorkspaceTaskStatus.Failed,
            _ => WorkspaceTaskStatus.Pending
        };
    }

    private static bool MatchStatus(WorkspaceTaskStatus current, WorkspaceTaskStatus? filter)
    {
        return filter is null || current == filter.Value;
    }

    private static bool MatchKeyword(string name, string? keyword)
    {
        return string.IsNullOrWhiteSpace(keyword)
            || name.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
