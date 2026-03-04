using Atlas.Application.Approval.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批流定义命令服务接口
/// </summary>
public interface IApprovalFlowCommandService
{
    Task<ApprovalFlowDefinitionResponse> CreateAsync(
        TenantId tenantId,
        ApprovalFlowDefinitionCreateRequest request,
        CancellationToken cancellationToken);

    Task<ApprovalFlowDefinitionResponse> UpdateAsync(
        TenantId tenantId,
        ApprovalFlowDefinitionUpdateRequest request,
        CancellationToken cancellationToken);

    Task PublishAsync(
        TenantId tenantId,
        long flowId,
        long publishedByUserId,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task DisableAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<ApprovalFlowDefinitionResponse> CopyAsync(
        TenantId tenantId,
        long id,
        ApprovalFlowCopyRequest request,
        CancellationToken cancellationToken);

    Task<ApprovalFlowDefinitionResponse> ImportAsync(
        TenantId tenantId,
        ApprovalFlowImportRequest request,
        CancellationToken cancellationToken);
}
