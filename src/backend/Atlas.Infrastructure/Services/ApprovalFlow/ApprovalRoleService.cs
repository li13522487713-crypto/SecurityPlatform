using Atlas.Application.Approval.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.ApprovalFlow;

/// <summary>
/// 审批模块角色查询服务默认实现（基于仓储）
/// </summary>
public sealed class ApprovalRoleService : IApprovalRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly ISqlSugarClient _db;

    public ApprovalRoleService(
        IRoleRepository roleRepository,
        ISqlSugarClient db)
    {
        _roleRepository = roleRepository;
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

    public async Task<IReadOnlyList<long>> GetUserIdsByRoleCodesAsync(
        TenantId tenantId,
        IReadOnlyList<string> roleCodes,
        CancellationToken cancellationToken)
    {
        if (roleCodes.Count == 0)
        {
            return Array.Empty<long>();
        }

        var roles = await _roleRepository.QueryByCodesAsync(tenantId, roleCodes, cancellationToken);
        if (roles.Count == 0)
        {
            return Array.Empty<long>();
        }

        var roleIds = roles.Select(x => x.Id).Distinct().ToArray();
        var userIds = await _db.Queryable<UserRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(roleIds, x.RoleId))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return userIds;
    }
}
