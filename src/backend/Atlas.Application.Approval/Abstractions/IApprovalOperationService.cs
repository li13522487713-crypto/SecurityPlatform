using Atlas.Application.Approval.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批流运行时操作服务接口
/// </summary>
public interface IApprovalOperationService
{
    Task ExecuteOperationAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        Models.ApprovalOperationRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 记录UI操作（预览/打印等，不改变流程状态）
    /// </summary>
    Task RecordUiOperationAsync(
        TenantId tenantId,
        long instanceId,
        long? taskId,
        long operatorUserId,
        ApprovalOperationType operationType,
        CancellationToken cancellationToken);
}
