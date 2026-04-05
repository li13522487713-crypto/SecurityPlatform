using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Approval.Repositories;
using Atlas.Application.Identity.Repositories;
using Atlas.Application.Platform.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批模块用户查询服务默认实现（基于仓储）
/// </summary>
public sealed class ApprovalUserService : IApprovalUserService
{
    private const int MaxRecursionCap = 100;

    private readonly IUserDepartmentRepository _userDepartmentRepository;
    private readonly IApprovalDepartmentLeaderRepository _deptLeaderRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserHierarchyQueryRepository _hierarchyQueryRepository;
    private readonly IAppMemberDepartmentRepository _appMemberDepartmentRepository;
    private readonly IAppDepartmentRepository _appDepartmentRepository;
    private readonly ISqlSugarClient _db;

    public ApprovalUserService(
        IUserDepartmentRepository userDepartmentRepository,
        IApprovalDepartmentLeaderRepository deptLeaderRepository,
        IRoleRepository roleRepository,
        IUserHierarchyQueryRepository hierarchyQueryRepository,
        IAppMemberDepartmentRepository appMemberDepartmentRepository,
        IAppDepartmentRepository appDepartmentRepository,
        ISqlSugarClient db)
    {
        _userDepartmentRepository = userDepartmentRepository;
        _deptLeaderRepository = deptLeaderRepository;
        _roleRepository = roleRepository;
        _hierarchyQueryRepository = hierarchyQueryRepository;
        _appMemberDepartmentRepository = appMemberDepartmentRepository;
        _appDepartmentRepository = appDepartmentRepository;
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
        if (appId is > 0)
        {
            var appIdValue = appId.Value;
            var userDepts = await _appMemberDepartmentRepository.QueryByUserIdAsync(
                tenantId, appIdValue, userId, cancellationToken);
            var primaryDept = userDepts.FirstOrDefault(x => x.IsPrimary) ?? userDepts.FirstOrDefault();
            if (primaryDept == null)
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
        if (primaryDeptPlatform == null)
        {
            return null;
        }

        // 获取部门负责人作为直属领导
        var leaderId = await _deptLeaderRepository.GetLeaderUserIdAsync(tenantId, primaryDeptPlatform.DepartmentId, cancellationToken);
        return leaderId;
    }

    public async Task<IReadOnlyList<long>> GetLoopApproversAsync(
        TenantId tenantId,
        long startUserId,
        int maxLevels = 10,
        CancellationToken cancellationToken = default,
        long? appId = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (maxLevels <= 0)
        {
            return Array.Empty<long>();
        }

        if (appId is > 0)
        {
            var depth = Math.Min(maxLevels, MaxRecursionCap);
            return await BuildAppLeaderChainAsync(tenantId, appId.Value, startUserId, depth, cancellationToken);
        }

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
        cancellationToken.ThrowIfCancellationRequested();
        if (targetLevel < 1)
        {
            return null;
        }

        if (appId is > 0)
        {
            var depth = Math.Min(targetLevel, MaxRecursionCap);
            var rows = await BuildAppLeaderChainAsync(tenantId, appId.Value, startUserId, depth, cancellationToken);
            return rows.Count >= targetLevel ? rows[targetLevel - 1] : null;
        }

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

    /// <summary>
    /// 与平台 <c>UserHierarchyQueryRepository</c> 一致：按用户主部门 → 部门负责人链向上解析，成员关系来自应用内成员部门表。
    /// </summary>
    private async Task<IReadOnlyList<long>> BuildAppLeaderChainAsync(
        TenantId tenantId,
        long appId,
        long userId,
        int maxLevels,
        CancellationToken cancellationToken)
    {
        var tenantGuid = tenantId.Value;
        var appMemberDepts = await _appMemberDepartmentRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var appDepts = await _appDepartmentRepository.QueryByAppIdAsync(tenantId, appId, cancellationToken);
        var appDeptIds = appDepts.Select(x => x.Id).ToHashSet();

        var departmentLeaders = await _db.Queryable<ApprovalDepartmentLeader>()
            .Where(x => x.TenantIdValue == tenantGuid)
            .ToListAsync(cancellationToken);

        var primaryDepartmentByUser = appMemberDepts
            .GroupBy(x => x.UserId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var primary = group.FirstOrDefault(x => x.IsPrimary);
                    if (primary is not null)
                    {
                        return primary.DepartmentId;
                    }

                    return group.Min(x => x.DepartmentId);
                });

        var leaderByDepartment = departmentLeaders
            .GroupBy(x => x.DepartmentId)
            .ToDictionary(g => g.Key, g => g.First().LeaderUserId);

        var leaders = new List<long>(maxLevels);
        var visitedUsers = new HashSet<long> { userId };
        var currentUserId = userId;
        var depth = 0;
        while (depth < maxLevels)
        {
            if (!primaryDepartmentByUser.TryGetValue(currentUserId, out var departmentId))
            {
                break;
            }

            if (!appDeptIds.Contains(departmentId))
            {
                break;
            }

            if (!leaderByDepartment.TryGetValue(departmentId, out var leaderUserId))
            {
                break;
            }

            if (!visitedUsers.Add(leaderUserId))
            {
                break;
            }

            leaders.Add(leaderUserId);
            currentUserId = leaderUserId;
            depth++;
        }

        return leaders;
    }
}
