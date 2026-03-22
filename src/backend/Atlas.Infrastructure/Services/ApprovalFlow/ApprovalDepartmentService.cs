using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批模块部门查询服务默认实现（基于仓储）
/// </summary>
public sealed class ApprovalDepartmentService : IApprovalDepartmentService
{
    private readonly IApprovalDepartmentLeaderRepository _deptLeaderRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;

    public ApprovalDepartmentService(
        IApprovalDepartmentLeaderRepository deptLeaderRepository,
        IUserDepartmentRepository userDepartmentRepository)
    {
        _deptLeaderRepository = deptLeaderRepository;
        _userDepartmentRepository = userDepartmentRepository;
    }

    public async Task<long?> GetLeaderUserIdAsync(
        TenantId tenantId,
        long departmentId,
        CancellationToken cancellationToken)
    {
        return await _deptLeaderRepository.GetLeaderUserIdAsync(tenantId, departmentId, cancellationToken);
    }

    public async Task<long?> GetDepartmentHeadUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken,
        long? appId = null)
    {
        // TODO[ORG-ENGINE]: appId 非 null 时应查询 AppDepartment 的负责人，待 AppMemberDepartment 关系表就绪后实现
        var userDepts = await _userDepartmentRepository.QueryByUserIdAsync(tenantId, userId, cancellationToken);
        var primaryDept = userDepts.FirstOrDefault(x => x.IsPrimary) ?? userDepts.FirstOrDefault();
        if (primaryDept is null)
        {
            return null;
        }

        return await _deptLeaderRepository.GetLeaderUserIdAsync(tenantId, primaryDept.DepartmentId, cancellationToken);
    }
}
