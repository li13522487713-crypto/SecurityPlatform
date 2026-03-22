using Atlas.Application.Abstractions;
using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批模块用户查询服务默认实现（基于仓储）
/// </summary>
public sealed class ApprovalUserService : IApprovalUserService
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IApprovalDepartmentLeaderRepository _deptLeaderRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserHierarchyQueryRepository _hierarchyQueryRepository;
    private readonly ISqlSugarClient _db;

    public ApprovalUserService(
        IUserAccountRepository userRepository,
        IUserDepartmentRepository userDepartmentRepository,
        IApprovalDepartmentLeaderRepository deptLeaderRepository,
        IRoleRepository roleRepository,
        IUserHierarchyQueryRepository hierarchyQueryRepository,
        ISqlSugarClient db)
    {
        _userRepository = userRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _deptLeaderRepository = deptLeaderRepository;
        _roleRepository = roleRepository;
        _hierarchyQueryRepository = hierarchyQueryRepository;
        _db = db;
    }

    public async Task<IReadOnlyList<long>> ValidateUserIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return Array.Empty<long>();
        }

        var userIdArray = userIds.Distinct().ToArray();
        // 批量查询用户，避免N+1查询
        var users = await _db.Queryable<UserAccount>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(userIdArray, x.Id) && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        return users;
    }

    public async Task<long?> GetDirectLeaderUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken,
        long? appId = null)
    {
        // TODO[ORG-ENGINE]: appId 非 null 时应查询 AppDepartment 的负责人，待 AppMemberDepartment 关系表就绪后实现
        var userDepts = await _userDepartmentRepository.QueryByUserIdAsync(tenantId, userId, cancellationToken);
        var primaryDept = userDepts.FirstOrDefault(x => x.IsPrimary) ?? userDepts.FirstOrDefault();
        if (primaryDept == null)
        {
            return null;
        }

        // 获取部门负责人作为直属领导
        var leaderId = await _deptLeaderRepository.GetLeaderUserIdAsync(tenantId, primaryDept.DepartmentId, cancellationToken);
        return leaderId;
    }

    public async Task<IReadOnlyList<long>> GetLoopApproversAsync(
        TenantId tenantId,
        long startUserId,
        int maxLevels = 10,
        CancellationToken cancellationToken = default,
        long? appId = null)
    {
        // TODO[ORG-ENGINE]: appId 非 null 时应沿 AppDepartment 层级链向上查找，待 AppMemberDepartment 关系表就绪后实现
        return await _hierarchyQueryRepository.GetLeaderChainAsync(
            tenantId,
            startUserId,
            maxLevels,
            cancellationToken);
    }

    public async Task<long?> GetLevelApproverAsync(
        TenantId tenantId,
        long startUserId,
        int targetLevel,
        CancellationToken cancellationToken = default,
        long? appId = null)
    {
        // TODO[ORG-ENGINE]: appId 非 null 时应按 AppDepartment 层级定位指定层级审批人，待 AppMemberDepartment 关系表就绪后实现
        return await _hierarchyQueryRepository.GetLeaderAtLevelAsync(
            tenantId,
            startUserId,
            targetLevel,
            cancellationToken);
    }

    public async Task<long?> GetHrbpUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken,
        long? appId = null)
    {
        // HRBP 策略：当前实现中，可以通过以下方式之一获取：
        // 1. 从用户扩展字段中获取
        // 2. 从部门配置中获取
        // 3. 从角色中获取（例如：HRBP角色）
        // 
        // 当前简化实现：查找用户所在部门关联的HRBP角色用户
        var hrbpRole = await _roleRepository.FindByCodeAsync(tenantId, "HRBP", cancellationToken);
        if (hrbpRole == null)
        {
            return null;
        }

        // 获取用户所在部门
        var userDepts = await _userDepartmentRepository.QueryByUserIdAsync(tenantId, userId, cancellationToken);
        if (userDepts.Count == 0)
        {
            return null;
        }

        // 查找同一部门中具有HRBP角色的用户（简化实现）
        // 实际应该根据业务规则：可能是部门关联的HRBP，或组织架构中的HRBP
        var deptIds = userDepts.Select(x => x.DepartmentId).ToList();
        if (deptIds.Count == 0)
        {
            return null;
        }

        var deptIdArray = deptIds.Distinct().ToArray();
        var hrbpUserIds = await _db.Queryable<UserRole>()
            .InnerJoin<UserDepartment>((ur, ud) => ur.UserId == ud.UserId)
            .Where((ur, ud) => 
                ur.TenantIdValue == tenantId.Value && 
                ur.RoleId == hrbpRole.Id &&
                SqlFunc.ContainsArray(deptIdArray, ud.DepartmentId))
            .Select((ur, ud) => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return hrbpUserIds.FirstOrDefault();
    }
}
