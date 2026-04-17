using Atlas.Core.Tenancy;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批实例事件负载。
/// </summary>
public sealed record ApprovalInstanceEvent(
    TenantId TenantId,
    long InstanceId,
    long DefinitionId,
    string BusinessKey,
    string? DataJson,
    long ActorUserId);

/// <summary>
/// 审批任务事件负载。
/// </summary>
public sealed record ApprovalTaskEvent(
    TenantId TenantId,
    long InstanceId,
    long TaskId,
    string NodeId,
    string BusinessKey,
    long ActorUserId,
    string? Comment);

/// <summary>
/// 审批事件处理器抽象：外部有界上下文可实现此接口订阅审批状态变化。
/// 当前仓库没有实现类（Phase 1-3 清理遗留），保留接口便于未来业务模块接入。
/// </summary>
public interface IApprovalEventHandler
{
    Task OnInstanceStartedAsync(ApprovalInstanceEvent e, CancellationToken cancellationToken);
    Task OnInstanceCompletedAsync(ApprovalInstanceEvent e, CancellationToken cancellationToken);
    Task OnInstanceRejectedAsync(ApprovalInstanceEvent e, CancellationToken cancellationToken);
    Task OnInstanceCanceledAsync(ApprovalInstanceEvent e, CancellationToken cancellationToken);
    Task OnTaskApprovedAsync(ApprovalTaskEvent e, CancellationToken cancellationToken);
    Task OnTaskRejectedAsync(ApprovalTaskEvent e, CancellationToken cancellationToken);
}
