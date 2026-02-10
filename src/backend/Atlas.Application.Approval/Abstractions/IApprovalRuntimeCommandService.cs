using Atlas.Application.Approval.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批流运行时命令服务接口
/// </summary>
public interface IApprovalRuntimeCommandService
{
    Task<ApprovalInstanceResponse> StartAsync(
        TenantId tenantId,
        ApprovalStartRequest request,
        long initiatorUserId,
        CancellationToken cancellationToken);

    Task ApproveTaskAsync(
        TenantId tenantId,
        long taskId,
        long approverUserId,
        string? comment,
        CancellationToken cancellationToken);

    Task RejectTaskAsync(
        TenantId tenantId,
        long taskId,
        long approverUserId,
        string? comment,
        CancellationToken cancellationToken);

    Task CancelInstanceAsync(
        TenantId tenantId,
        long instanceId,
        long cancelledByUserId,
        CancellationToken cancellationToken);

    Task MarkCopyRecordAsReadAsync(
        TenantId tenantId,
        long copyRecordId,
        long userId,
        CancellationToken cancellationToken);

    Task DelegateTaskAsync(
        TenantId tenantId,
        long taskId,
        long delegatorUserId,
        long delegateeUserId,
        string? comment,
        CancellationToken cancellationToken);

    Task ResolveTaskAsync(
        TenantId tenantId,
        long taskId,
        long resolverUserId,
        string? comment,
        CancellationToken cancellationToken);

    Task StartSubProcessAsync(
        TenantId tenantId,
        long parentInstanceId,
        string parentNodeId,
        long childProcessId,
        bool isAsync,
        CancellationToken cancellationToken);

    Task SuspendInstanceAsync(
        TenantId tenantId,
        long instanceId,
        long operatorUserId,
        CancellationToken cancellationToken);

    Task ActivateInstanceAsync(
        TenantId tenantId,
        long instanceId,
        long operatorUserId,
        CancellationToken cancellationToken);

    Task TerminateInstanceAsync(
        TenantId tenantId,
        long instanceId,
        long operatorUserId,
        string? comment,
        CancellationToken cancellationToken);

    Task<ApprovalInstanceResponse> SaveDraftAsync(
        TenantId tenantId,
        ApprovalStartRequest request,
        long initiatorUserId,
        CancellationToken cancellationToken);

    Task<ApprovalInstanceResponse> SubmitDraftAsync(
        TenantId tenantId,
        long instanceId,
        long initiatorUserId,
        CancellationToken cancellationToken);
}
