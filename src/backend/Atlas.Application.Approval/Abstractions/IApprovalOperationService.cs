using Atlas.Application.Approval.Models;
using Atlas.Core.Tenancy;

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
}
