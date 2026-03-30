using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.Application.Identity.Abstractions;

public interface IRbacResolver
{
    Task<IReadOnlyList<string>> GetRoleCodesAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetRoleCodesAsync(
        UserAccount account,
        TenantId tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetPermissionCodesAsync(
        TenantId tenantId,
        long userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// 一次 UserRole 查询同时返回角色编码与权限编码，
    /// 内部并行拉取 Role 列表和 RolePermission 列表，消除重复查询。
    /// </summary>
    Task<(IReadOnlyList<string> RoleCodes, IReadOnlyList<string> PermissionCodes)> GetRolesAndPermissionsAsync(
        UserAccount account,
        TenantId tenantId,
        CancellationToken cancellationToken);
}
