using Atlas.Application.Identity;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Enums;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;

namespace Atlas.Infrastructure.Services.Platform;

/// <summary>
/// 应用 Bootstrap 服务：创建应用时播种默认角色、权限、根部门，
/// 并将创建者设为 AppAdmin + 根部门主负责人。
/// </summary>
public sealed class AppBootstrapService : IAppBootstrapService
{
    private readonly IAppRoleRepository _roleRepository;
    private readonly IAppPermissionRepository _permissionRepository;
    private readonly IAppRolePermissionRepository _rolePermissionRepository;
    private readonly IAppUserRoleRepository _userRoleRepository;
    private readonly IAppMemberRepository _memberRepository;
    private readonly IAppMemberDepartmentRepository _memberDeptRepository;
    private readonly IAppDepartmentRepository _departmentRepository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public AppBootstrapService(
        IAppRoleRepository roleRepository,
        IAppPermissionRepository permissionRepository,
        IAppRolePermissionRepository rolePermissionRepository,
        IAppUserRoleRepository userRoleRepository,
        IAppMemberRepository memberRepository,
        IAppMemberDepartmentRepository memberDeptRepository,
        IAppDepartmentRepository departmentRepository,
        IIdGeneratorAccessor idGenerator)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _userRoleRepository = userRoleRepository;
        _memberRepository = memberRepository;
        _memberDeptRepository = memberDeptRepository;
        _departmentRepository = departmentRepository;
        _idGenerator = idGenerator;
    }

    public async Task BootstrapAsync(TenantId tenantId, long appId, long creatorUserId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var permissions = new List<AppPermission>(AppPermissionSeedCatalog.PermissionSeeds.Count);
        foreach (var seed in AppPermissionSeedCatalog.PermissionSeeds)
        {
            permissions.Add(new AppPermission(tenantId, appId, seed.Name, seed.Code, seed.Type, _idGenerator.NextId()));
        }
        foreach (var perm in permissions)
        {
            await _permissionRepository.AddAsync(perm, cancellationToken);
        }

        var adminRole = new AppRole(tenantId, appId, "AppAdmin", "应用管理员", "应用全局管理员，拥有全部权限", true, creatorUserId, now, _idGenerator.NextId());
        adminRole.SetDataScope(DataScopeType.All);
        await _roleRepository.AddAsync(adminRole, cancellationToken);

        var memberRole = new AppRole(tenantId, appId, "AppMember", "应用成员", "应用普通成员，仅可访问本人数据", true, creatorUserId, now, _idGenerator.NextId());
        memberRole.SetDataScope(DataScopeType.OnlySelf);
        await _roleRepository.AddAsync(memberRole, cancellationToken);

        var adminRolePermissions = permissions.Select(p =>
            new AppRolePermission(tenantId, appId, adminRole.Id, p.Code, _idGenerator.NextId())).ToList();
        await _rolePermissionRepository.AddRangeAsync(adminRolePermissions, cancellationToken);

        var memberRolePermissions = permissions
            .Where(p => AppPermissionSeedCatalog.GetPermissionCodesForRole(memberRole.Code)
                .Contains(p.Code, StringComparer.OrdinalIgnoreCase))
            .Select(p => new AppRolePermission(tenantId, appId, memberRole.Id, p.Code, _idGenerator.NextId()))
            .ToList();
        await _rolePermissionRepository.AddRangeAsync(memberRolePermissions, cancellationToken);

        var rootDept = new AppDepartment(tenantId, appId, "默认部门", "root", null, 1, _idGenerator.NextId());
        await _departmentRepository.AddAsync(rootDept, cancellationToken);

        var creatorMember = new AppMember(tenantId, appId, creatorUserId, creatorUserId, now, _idGenerator.NextId());
        await _memberRepository.AddRangeAsync([creatorMember], cancellationToken);

        var creatorAdminUserRole = new AppUserRole(tenantId, appId, creatorUserId, adminRole.Id, _idGenerator.NextId());
        await _userRoleRepository.AddRangeAsync([creatorAdminUserRole], cancellationToken);

        var creatorDept = new AppMemberDepartment(tenantId, appId, creatorUserId, rootDept.Id, true, _idGenerator.NextId());
        await _memberDeptRepository.AddRangeAsync([creatorDept], cancellationToken);
    }
}
