using Atlas.Application.Approval.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批模块用户查询服务实现（组合式实现，依赖接口契约）
/// 
/// 此实现通过组合 IApprovalUserService、IApprovalRoleService、IApprovalDepartmentService
/// 来提供完整的审批人查询能力，实现了接口契约与具体实现的解耦。
/// 
/// 替换方式：
/// 1. 实现 IApprovalUserService、IApprovalRoleService、IApprovalDepartmentService 接口
/// 2. 在 DI 容器中注册自定义实现
/// 3. ApprovalUserQueryService 会自动使用新的实现
/// </summary>
public sealed class ApprovalUserQueryService : IApprovalUserQueryService
{
    private readonly IApprovalUserService _userService;
    private readonly IApprovalRoleService _roleService;
    private readonly IApprovalDepartmentService _departmentService;

    public ApprovalUserQueryService(
        IApprovalUserService userService,
        IApprovalRoleService roleService,
        IApprovalDepartmentService departmentService)
    {
        _userService = userService;
        _roleService = roleService;
        _departmentService = departmentService;
    }

    public async Task<IReadOnlyList<long>> GetUserIdsByRoleCodeAsync(
        TenantId tenantId,
        string roleCode,
        CancellationToken cancellationToken)
    {
        return await _roleService.GetUserIdsByRoleCodeAsync(tenantId, roleCode, cancellationToken);
    }

    public async Task<long?> GetDirectLeaderUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        return await _userService.GetDirectLeaderUserIdAsync(tenantId, userId, cancellationToken);
    }

    public async Task<IReadOnlyList<long>> GetLoopApproversAsync(
        TenantId tenantId,
        long startUserId,
        int maxLevels = 10,
        CancellationToken cancellationToken = default)
    {
        return await _userService.GetLoopApproversAsync(tenantId, startUserId, maxLevels, cancellationToken);
    }

    public async Task<long?> GetLevelApproverAsync(
        TenantId tenantId,
        long startUserId,
        int targetLevel,
        CancellationToken cancellationToken = default)
    {
        return await _userService.GetLevelApproverAsync(tenantId, startUserId, targetLevel, cancellationToken);
    }

    public async Task<long?> GetHrbpUserIdAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken)
    {
        return await _userService.GetHrbpUserIdAsync(tenantId, userId, cancellationToken);
    }

    public async Task<IReadOnlyList<long>> ValidateUserIdsAsync(
        TenantId tenantId,
        IReadOnlyList<long> userIds,
        CancellationToken cancellationToken)
    {
        return await _userService.ValidateUserIdsAsync(tenantId, userIds, cancellationToken);
    }
}
