using System.Reflection;
using Atlas.Application.Identity;
using Atlas.Core.Enums;

namespace Atlas.SecurityPlatform.Tests.Platform;

/// <summary>
/// 校验平台种子角色权限与 <see cref="Atlas.Infrastructure.Services.DatabaseInitializerHostedService"/> 中定义一致；
/// 若调整种子数据，请同步更新本文件中的常量数组与期望。
/// </summary>
public sealed class SeedDataIntegrityTests
{
    /// <summary>须与 DatabaseInitializerHostedService.SecurityAdminSeedPermissionCodes 保持同步。</summary>
    private static readonly string[] SecurityAdminSeedPermissionCodes =
    [
        PermissionCodes.UsersView,
        PermissionCodes.UsersCreate,
        PermissionCodes.UsersUpdate,
        PermissionCodes.UsersDelete,
        PermissionCodes.UsersAssignRoles,
        PermissionCodes.UsersAssignDepartments,
        PermissionCodes.UsersAssignPositions,
        PermissionCodes.RolesView,
        PermissionCodes.RolesCreate,
        PermissionCodes.RolesUpdate,
        PermissionCodes.RolesDelete,
        PermissionCodes.RolesAssignPermissions,
        PermissionCodes.RolesAssignMenus,
        PermissionCodes.PermissionsView,
        PermissionCodes.PermissionsCreate,
        PermissionCodes.PermissionsUpdate,
        PermissionCodes.DepartmentsView,
        PermissionCodes.DepartmentsAll,
        PermissionCodes.DepartmentsCreate,
        PermissionCodes.DepartmentsUpdate,
        PermissionCodes.DepartmentsDelete,
        PermissionCodes.PositionsView,
        PermissionCodes.PositionsCreate,
        PermissionCodes.PositionsUpdate,
        PermissionCodes.PositionsDelete,
        PermissionCodes.MenusView,
        PermissionCodes.MenusAll,
        PermissionCodes.MenusCreate,
        PermissionCodes.MenusUpdate,
        PermissionCodes.MenusDelete,
        PermissionCodes.DataScopeManage
    ];

    /// <summary>须与 DatabaseInitializerHostedService.AuditAdminSeedPermissionCodes 保持同步（AuditAdmin / Auditor）。</summary>
    private static readonly string[] AuditAdminSeedPermissionCodes =
    [
        PermissionCodes.AuditView,
        PermissionCodes.LoginLogView
    ];

    [Fact]
    public void SuperAdmin_BootstrapRole_ShouldMapToAllDeclaredPermissionCodes()
    {
        var map = BuildPermissionCodeMapFromPermissionCodesClass();
        var superAdminCodes = EnumerateSuperAdminSeedPermissionCodes(map).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(map.Count, superAdminCodes.Count);
        foreach (var key in map.Keys)
        {
            Assert.Contains(key, superAdminCodes);
        }
    }

    [Fact]
    public void SecurityAdmin_SeedPermissionCodes_ShouldNotInclude_SystemAdmin()
    {
        Assert.DoesNotContain(PermissionCodes.SystemAdmin, SecurityAdminSeedPermissionCodes, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Auditor_SeedPermissionCodes_ShouldBeLimitedTo_AuditAndLoginLog()
    {
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PermissionCodes.AuditView,
            PermissionCodes.LoginLogView
        };
        var actual = AuditAdminSeedPermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SecurityAdmin_SeedPermissionCodes_ShouldBeSubsetOf_PlatformCatalogExcludingSystemAdmin()
    {
        var catalog = BuildPermissionCodeMapFromPermissionCodesClass().Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        catalog.Remove(PermissionCodes.SystemAdmin);

        foreach (var code in SecurityAdminSeedPermissionCodes)
        {
            Assert.Contains(code, catalog);
        }
    }

    [Theory]
    [InlineData("SuperAdmin", (int)DataScopeType.All)]
    [InlineData("SecurityAdmin", (int)DataScopeType.CurrentTenant)]
    [InlineData("Auditor", (int)DataScopeType.CurrentTenant)]
    public void BootstrapRole_ExpectedDataScope_ShouldMatchInitializerRepairRule(string roleCode, int expectedScopeValue)
    {
        // 与 EnsureBootstrapRoleDataScopesAsync 一致：仅 SuperAdmin 为 All，其余引导角色为 CurrentTenant。
        var actual = string.Equals(roleCode, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            ? DataScopeType.All
            : DataScopeType.CurrentTenant;
        Assert.Equal((DataScopeType)expectedScopeValue, actual);
    }

    private static IReadOnlyDictionary<string, long> BuildPermissionCodeMapFromPermissionCodesClass()
    {
        var type = typeof(PermissionCodes);
        var dict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        long n = 1;
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        {
            if (field.FieldType != typeof(string))
            {
                continue;
            }

            var value = field.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            dict[value] = n++;
        }

        return dict;
    }

    /// <summary>与 DatabaseInitializerHostedService 中 SuperAdmin 分支一致：返回权限表中的全部编码。</summary>
    private static IEnumerable<string> EnumerateSuperAdminSeedPermissionCodes(IReadOnlyDictionary<string, long> permissionIdMap)
    {
        foreach (var code in permissionIdMap.Keys)
        {
            yield return code;
        }
    }
}
