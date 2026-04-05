using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Platform.Repositories;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批模块部门查询服务默认实现（基于仓储）
/// </summary>
public sealed class ApprovalDepartmentService : IApprovalDepartmentService
{
    private readonly IApprovalDepartmentLeaderRepository _deptLeaderRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IAppMemberDepartmentRepository _appMemberDepartmentRepository;
    private readonly IAppDepartmentRepository _appDepartmentRepository;

    public ApprovalDepartmentService(
        IApprovalDepartmentLeaderRepository deptLeaderRepository,
        IUserDepartmentRepository userDepartmentRepository,
        IAppMemberDepartmentRepository appMemberDepartmentRepository,
        IAppDepartmentRepository appDepartmentRepository)
    {
        _deptLeaderRepository = deptLeaderRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _appMemberDepartmentRepository = appMemberDepartmentRepository;
        _appDepartmentRepository = appDepartmentRepository;
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
        if (appId is > 0)
        {
            var appIdValue = appId.Value;
            var userDepts = await _appMemberDepartmentRepository.QueryByUserIdAsync(
                tenantId, appIdValue, userId, cancellationToken);
            var primaryDept = userDepts.FirstOrDefault(x => x.IsPrimary) ?? userDepts.FirstOrDefault();
            if (primaryDept is null)
            {
                return null;
            }

            var appDepts = await _appDepartmentRepository.QueryByAppIdAsync(tenantId, appIdValue, cancellationToken);
            var appDeptIds = appDepts.Select(x => x.Id).ToHashSet();
            if (!appDeptIds.Contains(primaryDept.DepartmentId))
            {
                return null;
            }

            return await _deptLeaderRepository.GetLeaderUserIdAsync(
                tenantId, primaryDept.DepartmentId, cancellationToken);
        }

        var userDeptsPlatform = await _userDepartmentRepository.QueryByUserIdAsync(tenantId, userId, cancellationToken);
        var primaryDeptPlatform = userDeptsPlatform.FirstOrDefault(x => x.IsPrimary) ?? userDeptsPlatform.FirstOrDefault();
        if (primaryDeptPlatform is null)
        {
            return null;
        }

        return await _deptLeaderRepository.GetLeaderUserIdAsync(tenantId, primaryDeptPlatform.DepartmentId, cancellationToken);
    }
}
