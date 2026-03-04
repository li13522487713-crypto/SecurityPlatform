using Atlas.Application.Approval.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Enums;

namespace Atlas.Application.Approval.Abstractions;

/// <summary>
/// 审批流定义查询服务接口
/// </summary>
public interface IApprovalFlowQueryService
{
    Task<ApprovalFlowDefinitionResponse?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<PagedResult<ApprovalFlowDefinitionListItem>> GetPagedAsync(
        TenantId tenantId,
        PagedRequest request,
        ApprovalFlowStatus? status = null,
        string? keyword = null,
        CancellationToken cancellationToken = default);

    Task<ApprovalFlowExportResponse?> ExportAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken);

    Task<ApprovalFlowCompareResponse?> CompareAsync(
        TenantId tenantId,
        long id,
        int targetVersion,
        CancellationToken cancellationToken);
}
