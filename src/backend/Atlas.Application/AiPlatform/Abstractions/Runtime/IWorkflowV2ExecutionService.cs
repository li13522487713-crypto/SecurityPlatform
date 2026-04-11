using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

/// <summary>
/// V2 工作流执行服务（同步/异步运行、取消、恢复、流式运行、单节点调试）。
/// </summary>
public interface IWorkflowV2ExecutionService
{
    Task<WorkflowV2RunResult> SyncRunAsync(
        TenantId tenantId, long workflowId, long userId, WorkflowV2RunRequest request, CancellationToken cancellationToken);

    Task<WorkflowV2RunResult> AsyncRunAsync(
        TenantId tenantId, long workflowId, long userId, WorkflowV2RunRequest request, CancellationToken cancellationToken);

    Task CancelAsync(TenantId tenantId, long executionId, CancellationToken cancellationToken);

    Task ResumeAsync(TenantId tenantId, long executionId, CancellationToken cancellationToken);
    Task ResumeAsync(TenantId tenantId, long executionId, WorkflowV2ResumeRequest? request, CancellationToken cancellationToken);

    Task<WorkflowV2RunResult> DebugNodeAsync(
        TenantId tenantId, long workflowId, long userId, WorkflowV2NodeDebugRequest request, CancellationToken cancellationToken);

    IAsyncEnumerable<SseEvent> StreamRunAsync(
        TenantId tenantId, long workflowId, long userId, WorkflowV2RunRequest request, CancellationToken cancellationToken);

    IAsyncEnumerable<SseEvent> StreamResumeAsync(
        TenantId tenantId, long executionId, CancellationToken cancellationToken);
}
