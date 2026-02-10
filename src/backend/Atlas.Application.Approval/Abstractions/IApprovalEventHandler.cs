using Atlas.Core.Tenancy;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批流领域事件处理器接口。
/// 外部业务模块（如动态表）通过实现此接口来响应审批流的状态变更，
/// 实现审批模块与业务模块的解耦。
/// </summary>
public interface IApprovalEventHandler
{
    /// <summary>流程实例启动</summary>
    Task OnInstanceStartedAsync(ApprovalInstanceEvent e, CancellationToken ct) => Task.CompletedTask;

    /// <summary>流程实例完成（审批通过）</summary>
    Task OnInstanceCompletedAsync(ApprovalInstanceEvent e, CancellationToken ct) => Task.CompletedTask;

    /// <summary>流程实例被驳回</summary>
    Task OnInstanceRejectedAsync(ApprovalInstanceEvent e, CancellationToken ct) => Task.CompletedTask;

    /// <summary>流程实例被取消</summary>
    Task OnInstanceCanceledAsync(ApprovalInstanceEvent e, CancellationToken ct) => Task.CompletedTask;

    /// <summary>任务被审批通过</summary>
    Task OnTaskApprovedAsync(ApprovalTaskEvent e, CancellationToken ct) => Task.CompletedTask;

    /// <summary>任务被驳回</summary>
    Task OnTaskRejectedAsync(ApprovalTaskEvent e, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// 审批流实例事件数据
/// </summary>
public record ApprovalInstanceEvent(
    TenantId TenantId,
    long InstanceId,
    long DefinitionId,
    string BusinessKey,
    string? DataJson,
    long ActorUserId);

/// <summary>
/// 审批流任务事件数据
/// </summary>
public record ApprovalTaskEvent(
    TenantId TenantId,
    long InstanceId,
    long TaskId,
    string NodeId,
    string BusinessKey,
    long ActorUserId,
    string? Comment);
