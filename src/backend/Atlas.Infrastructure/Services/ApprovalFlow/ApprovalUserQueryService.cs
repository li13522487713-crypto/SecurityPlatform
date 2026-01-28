using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批模块用户查询服务实现
/// </summary>
public sealed class ApprovalUserQueryService : IApprovalUserQueryService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IApprovalDepartmentLeaderRepository _deptLeaderRepository;
    private readonly IUserAccountRepository _userRepository;
    private readonly ISqlSugarClient _db;

    public ApprovalUserQueryService(
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IUserDepartmentRepository userDepartmentRepository,
        IDepartmentRepository departmentRepository,
        IApprovalDepartmentLeaderRepository deptLeaderRepository,
        IUserAccountRepository userRepository,
        ISqlSugarClient db)
    {
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _userDepartmentRepository = userDepartmentRepository;
        _departmentRepository = departmentRepository;
        _deptLeaderRepository = deptLeaderRepository;
        _userRepository = userRepository;
        _db = db;
    }

    public async Task<IReadOnlyList<long>> GetUserIdsByRoleCodeAsync(
        TenantId tenantId,
        string roleCode,
        CancellationToken cancellationToken)
    {
        var role = await _roleRepository.FindByCodeAsync(tenantId, roleCode, cancellationToken);
        if (role == null)
        {
            return Array.Empty<long>();
        }

        // 通过数据库查询具有该角色的所有用户ID
        var userIds = await _db.Queryable<UserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.RoleId == role.Id)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return userIds;
    }

    public async Task<long?> GetDirectLeaderUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        // 获取用户的主部门
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
        CancellationToken cancellationToken = default)
    {
        var approvers = new List<long>();
        var currentUserId = startUserId;
        var visited = new HashSet<long> { currentUserId }; // 防止循环

        for (int level = 0; level < maxLevels; level++)
        {
            var leaderId = await GetDirectLeaderUserIdAsync(tenantId, currentUserId, cancellationToken);
            if (!leaderId.HasValue || visited.Contains(leaderId.Value))
            {
                // 没有找到上级或出现循环，停止查找
                break;
            }

            approvers.Add(leaderId.Value);
            visited.Add(leaderId.Value);
            currentUserId = leaderId.Value;
        }

        return approvers;
    }

    public async Task<long?> GetLevelApproverAsync(
        TenantId tenantId,
        long startUserId,
        int targetLevel,
        CancellationToken cancellationToken = default)
    {
        if (targetLevel < 1)
        {
            return null;
        }

        var currentUserId = startUserId;
        var visited = new HashSet<long> { currentUserId }; // 防止循环

        for (int level = 1; level <= targetLevel; level++)
        {
            var leaderId = await GetDirectLeaderUserIdAsync(tenantId, currentUserId, cancellationToken);
            if (!leaderId.HasValue || visited.Contains(leaderId.Value))
            {
                // 层级不足或出现循环
                return null;
            }

            if (level == targetLevel)
            {
                return leaderId.Value;
            }

            visited.Add(leaderId.Value);
            currentUserId = leaderId.Value;
        }

        return null;
    }

    public async Task<long?> GetHrbpUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
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
        var hrbpUserIds = await _db.Queryable<UserRole>()
            .InnerJoin<UserDepartment>((ur, ud) => ur.UserId == ud.UserId)
            .Where((ur, ud) => 
                ur.TenantIdValue == tenantId.Value && 
                ur.RoleId == hrbpRole.Id &&
                deptIds.Contains(ud.DepartmentId))
            .Select((ur, ud) => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return hrbpUserIds.FirstOrDefault();
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

        var validUserIds = new List<long>();
        foreach (var userId in userIds)
        {
            var user = await _userRepository.FindByIdAsync(tenantId, userId, cancellationToken);
            if (user != null && user.IsActive)
            {
                validUserIds.Add(userId);
            }
        }

        return validUserIds;
    }
}
