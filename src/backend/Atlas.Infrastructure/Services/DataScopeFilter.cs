using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Enums;
using Atlas.Core.Identity;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services;

/// <summary>
/// 基于当前用户角色的数据权限过滤器实现（等保2.0 访问控制）
/// </summary>
public sealed class DataScopeFilter : IDataScopeFilter
{
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly RoleRepository _roleRepository;

    public DataScopeFilter(
        ICurrentUserAccessor currentUserAccessor,
        RoleRepository roleRepository)
    {
        _currentUserAccessor = currentUserAccessor;
        _roleRepository = roleRepository;
    }

    public async Task<DataScopeType> GetEffectiveScopeAsync(CancellationToken ct = default)
    {
        var user = _currentUserAccessor.GetCurrentUser();
        if (user is null) return DataScopeType.OnlySelf;

        if (user.Roles.Count == 0) return DataScopeType.OnlySelf;

        var roles = await _roleRepository.QueryByCodesAsync(user.TenantId, user.Roles, ct);

        // 取最宽松的数据权限（数值越小越宽松）
        var minScope = roles.Count > 0
            ? roles.Min(r => r.DataScope)
            : DataScopeType.OnlySelf;

        return minScope;
    }

    public async Task<long?> GetOwnerFilterIdAsync(CancellationToken ct = default)
    {
        var scope = await GetEffectiveScopeAsync(ct);

        if (scope == DataScopeType.OnlySelf)
        {
            var user = _currentUserAccessor.GetCurrentUser();
            return user?.UserId;
        }

        return null; // null = 不过滤（有权查看所有）
    }
}
