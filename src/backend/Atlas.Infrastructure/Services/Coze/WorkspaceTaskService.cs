using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.Coze;

/// <summary>
/// 任务中心持久化版。
///
/// - M4.4：接入 <see cref="EvaluationTask"/>，按 tenantId 过滤。
/// - M5.1+5.2：EvaluationTask 新增 WorkspaceId 列后，严格按 (tenantId, workspaceId) 双键过滤；
///   历史无 WorkspaceId 的任务（旧 EvaluationService 创建）不会出现在任何 Coze 工作空间视图里。
///
/// 规划（M6）：多源聚合 BatchJobExecution + PersistedExecutionPointer + Hangfire job，
/// 需要先给相关 Entity 补 WorkspaceId 列。
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

        // 仅当 type 未指定或匹配 "evaluation" 时返回数据；其它 type（workflow/batch/publish）
        // 的数据源在 M6 多源聚合里接入。
        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, TaskType, StringComparison.OrdinalIgnoreCase))
        {
            return new PagedResult<WorkspaceTaskItemDto>(Array.Empty<WorkspaceTaskItemDto>(), 0, pageIndex, pageSize);
        }

        // M5.2：严格按 workspaceId 过滤。EvaluationTask.WorkspaceId 为 nullable，
        // 旧任务不会进入该集合；Coze API 创建的任务必须显式 AttachWorkspace(workspaceId)。
        var (entities, total) = await _evaluationTaskRepository.GetPagedByWorkspaceAsync(
            tenantId,
            workspaceId,
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);

        var items = entities
            .Select(MapItem)
            .Where(item => MatchStatus(item.Status, status))
            .ToArray();

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
        if (entity is null || !string.Equals(entity.WorkspaceId, workspaceId, StringComparison.Ordinal))
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
}
