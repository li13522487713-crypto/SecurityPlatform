using System.Collections.Concurrent;
using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.Coze;

/// <summary>
/// 任务中心（PRD 02-7.4）。第一阶段：无任务记录（Empty 状态）。
/// 第二阶段对接 BatchJobExecution / PersistedExecutionPointer / Hangfire job 表，
/// 通过统一聚合视图返回。
/// </summary>
public sealed class InMemoryWorkspaceTaskService : IWorkspaceTaskService
{
    public Task<PagedResult<WorkspaceTaskItemDto>> ListAsync(
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
        return Task.FromResult(new PagedResult<WorkspaceTaskItemDto>(
            Array.Empty<WorkspaceTaskItemDto>(),
            0,
            pageIndex,
            pageSize));
    }

    public Task<WorkspaceTaskDetailDto?> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string taskId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<WorkspaceTaskDetailDto?>(null);
    }
}

/// <summary>
/// 效果评测（PRD 02-7.5）。第一阶段：无评测记录。
/// 第二阶段直接对接 EvaluationDataset / EvaluationTask / EvaluationResult 现有 Domain。
/// </summary>
public sealed class InMemoryWorkspaceEvaluationService : IWorkspaceEvaluationService
{
    public Task<PagedResult<EvaluationItemDto>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var pageIndex = Math.Max(1, pagedRequest.PageIndex);
        var pageSize = Math.Clamp(pagedRequest.PageSize, 1, 100);
        return Task.FromResult(new PagedResult<EvaluationItemDto>(
            Array.Empty<EvaluationItemDto>(),
            0,
            pageIndex,
            pageSize));
    }

    public Task<EvaluationDetailDto?> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string evaluationId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<EvaluationDetailDto?>(null);
    }
}

/// <summary>
/// 测试集（PRD 05-4.8）。第一阶段：进程内 ConcurrentDictionary 存储，
/// 用于让前端测试集抽屉跑通"创建 → 列表"循环。
/// 第二阶段对接 EvaluationDataset/EvaluationCase 现有持久化模型。
/// </summary>
public sealed class InMemoryWorkspaceTestsetService : IWorkspaceTestsetService
{
    private static readonly ConcurrentDictionary<string, List<TestsetItemDto>> Store = new();

    private readonly IIdGeneratorAccessor _idGenerator;

    public InMemoryWorkspaceTestsetService(IIdGeneratorAccessor idGenerator)
    {
        _idGenerator = idGenerator;
    }

    public Task<PagedResult<TestsetItemDto>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var key = BuildKey(tenantId, workspaceId);
        var snapshot = Store.GetOrAdd(key, _ => new List<TestsetItemDto>());
        IReadOnlyList<TestsetItemDto> filtered;
        lock (snapshot)
        {
            filtered = snapshot
                .Where(item => string.IsNullOrWhiteSpace(keyword)
                    || item.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.CreatedAt)
                .ToArray();
        }

        var pageIndex = Math.Max(1, pagedRequest.PageIndex);
        var pageSize = Math.Clamp(pagedRequest.PageSize, 1, 100);
        var skip = (pageIndex - 1) * pageSize;
        var page = filtered.Skip(skip).Take(pageSize).ToArray();

        return Task.FromResult(new PagedResult<TestsetItemDto>(
            page,
            filtered.Count,
            pageIndex,
            pageSize));
    }

    public Task<string> CreateAsync(
        TenantId tenantId,
        string workspaceId,
        TestsetCreateRequest request,
        CancellationToken cancellationToken)
    {
        var key = BuildKey(tenantId, workspaceId);
        var id = _idGenerator.NextId().ToString();
        var item = new TestsetItemDto(
            Id: id,
            Name: request.Name.Trim(),
            Description: string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            WorkflowId: request.WorkflowId,
            RowCount: request.Rows?.Count ?? 0,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow);

        var bucket = Store.GetOrAdd(key, _ => new List<TestsetItemDto>());
        lock (bucket)
        {
            bucket.Add(item);
        }
        return Task.FromResult(id);
    }

    private static string BuildKey(TenantId tenantId, string workspaceId)
    {
        return $"{tenantId.Value:N}:{workspaceId}";
    }
}
