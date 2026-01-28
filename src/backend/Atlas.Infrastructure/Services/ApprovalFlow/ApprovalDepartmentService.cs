using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批模块部门查询服务默认实现（基于仓储）
/// </summary>
public sealed class ApprovalDepartmentService : IApprovalDepartmentService
{
    private readonly IApprovalDepartmentLeaderRepository _deptLeaderRepository;

    public ApprovalDepartmentService(
        IApprovalDepartmentLeaderRepository deptLeaderRepository)
    {
        _deptLeaderRepository = deptLeaderRepository;
    }

    public async Task<long?> GetLeaderUserIdAsync(
        TenantId tenantId,
        long departmentId,
        CancellationToken cancellationToken)
    {
        return await _deptLeaderRepository.GetLeaderUserIdAsync(tenantId, departmentId, cancellationToken);
    }
}
