using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

/// <summary>
/// V2 工作流执行服务（同步/异步运行、取消、恢复、流式运行、单节点调试）。
/// </summary>
public interface IDagWorkflowExecutionService
{
    Task<DagWorkflowRunResult> SyncRunAsync(
        TenantId tenantId, long workflowId, long userId, DagWorkflowRunRequest request, CancellationToken cancellationToken);

    Task<DagWorkflowRunResult> AsyncRunAsync(
        TenantId tenantId, long workflowId, long userId, DagWorkflowRunRequest request, CancellationToken cancellationToken);

    Task CancelAsync(TenantId tenantId, long executionId, CancellationToken cancellationToken);

    Task ResumeAsync(TenantId tenantId, long executionId, CancellationToken cancellationToken);
    Task ResumeAsync(TenantId tenantId, long executionId, DagWorkflowResumeRequest? request, CancellationToken cancellationToken);

    Task<DagWorkflowRunResult> DebugNodeAsync(
        TenantId tenantId, long workflowId, long userId, DagWorkflowNodeDebugRequest request, CancellationToken cancellationToken);

    IAsyncEnumerable<SseEvent> StreamRunAsync(
        TenantId tenantId, long workflowId, long userId, DagWorkflowRunRequest request, CancellationToken cancellationToken);

    IAsyncEnumerable<SseEvent> StreamResumeAsync(
        TenantId tenantId, long executionId, CancellationToken cancellationToken);
}
