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
}
