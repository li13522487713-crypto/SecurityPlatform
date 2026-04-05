using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Enums;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Services;
using NSubstitute;

namespace Atlas.SecurityPlatform.Tests.Platform;

public sealed class TenantDataScopeFilterTests
{
    private static TenantId TestTenant => new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public async Task GetEffectiveScopeAsync_MultipleRoles_ShouldUseMinimumEnumValue_MostPermissiveWins()
    {
        var user = new CurrentUserInfo(1, "u", "u", TestTenant, new[] { "Wide", "Narrow" }, false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var roleWide = new Role(TestTenant, "宽", "Wide", 10);
        var roleNarrow = new Role(TestTenant, "窄", "Narrow", 11);
        roleNarrow.SetDataScope(DataScopeType.OnlySelf);

        var roleRepo = Substitute.For<IRoleRepository>();
        roleRepo.QueryByCodesAsync(TestTenant, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { roleWide, roleNarrow });

        var sut = new TenantDataScopeFilter(
            accessor,
            roleRepo,
            Substitute.For<IRoleDeptRepository>(),
            Substitute.For<IUserDepartmentRepository>(),
            Substitute.For<IDepartmentRepository>(),
            Substitute.For<IProjectUserRepository>());

        var scope = await sut.GetEffectiveScopeAsync(CancellationToken.None);

        Assert.Equal(DataScopeType.CurrentTenant, scope);
    }

    [Fact]
    public async Task GetOwnerFilterIdAsync_WhenEffectiveScopeIsOnlySelf_ShouldReturnCurrentUserId()
    {
        const long userId = 42L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, new[] { "SelfOnly" }, false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var role = new Role(TestTenant, "仅本人", "SelfOnly", 20);
        role.SetDataScope(DataScopeType.OnlySelf);

        var roleRepo = Substitute.For<IRoleRepository>();
        roleRepo.QueryByCodesAsync(TestTenant, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { role });

        var sut = new TenantDataScopeFilter(
            accessor,
            roleRepo,
            Substitute.For<IRoleDeptRepository>(),
            Substitute.For<IUserDepartmentRepository>(),
            Substitute.For<IDepartmentRepository>(),
            Substitute.For<IProjectUserRepository>());

        var ownerId = await sut.GetOwnerFilterIdAsync(CancellationToken.None);

        Assert.Equal(userId, ownerId);
    }

    [Fact]
    public async Task GetDeptFilterIdsAsync_WhenCustomDept_ShouldReturnDistinctDeptIdsFromRoleAssignments()
    {
        var user = new CurrentUserInfo(7, "u", "u", TestTenant, new[] { "DeptCustom" }, false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var role = new Role(TestTenant, "自定义部门", "DeptCustom", 30);
        role.SetDataScope(DataScopeType.CustomDept);

        var roleRepo = Substitute.For<IRoleRepository>();
        roleRepo.QueryByCodesAsync(TestTenant, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { role });

        var roleDeptRepo = Substitute.For<IRoleDeptRepository>();
        roleDeptRepo.QueryByRoleIdsAsync(TestTenant, Arg.Is<IReadOnlyList<long>>(ids => ids.Count == 1 && ids[0] == 30), Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new RoleDept(TestTenant, 30, 100, 1),
                new RoleDept(TestTenant, 30, 200, 2),
                new RoleDept(TestTenant, 30, 100, 3)
            });

        var sut = new TenantDataScopeFilter(
            accessor,
            roleRepo,
            roleDeptRepo,
            Substitute.For<IUserDepartmentRepository>(),
            Substitute.For<IDepartmentRepository>(),
            Substitute.For<IProjectUserRepository>());

        var deptIds = await sut.GetDeptFilterIdsAsync(CancellationToken.None);

        Assert.NotNull(deptIds);
        Assert.Equal(2, deptIds!.Count);
        Assert.Contains(100L, deptIds);
        Assert.Contains(200L, deptIds);
    }

    [Fact]
    public async Task GetDeptFilterIdsAsync_WhenCurrentDeptAndBelow_ShouldIncludeDescendantDepartments()
    {
        var user = new CurrentUserInfo(9, "u", "u", TestTenant, new[] { "DeptTree" }, false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var role = new Role(TestTenant, "本部门及以下", "DeptTree", 40);
        role.SetDataScope(DataScopeType.CurrentDeptAndBelow);

        var roleRepo = Substitute.For<IRoleRepository>();
        roleRepo.QueryByCodesAsync(TestTenant, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { role });

        var userDeptRepo = Substitute.For<IUserDepartmentRepository>();
        userDeptRepo.QueryByUserIdAsync(TestTenant, 9, Arg.Any<CancellationToken>())
            .Returns(new[] { new UserDepartment(TestTenant, 9, 10, 1, true) });

        var root = new Department(TestTenant, "总部", "HQ", 10, null, 0);
        var child = new Department(TestTenant, "分部", "BR", 20, 10, 1);
        var grandChild = new Department(TestTenant, "小组", "TM", 30, 20, 2);

        var deptRepo = Substitute.For<IDepartmentRepository>();
        deptRepo.QueryAllAsync(TestTenant, Arg.Any<CancellationToken>())
            .Returns(new[] { root, child, grandChild });

        var sut = new TenantDataScopeFilter(
            accessor,
            roleRepo,
            Substitute.For<IRoleDeptRepository>(),
            userDeptRepo,
            deptRepo,
            Substitute.For<IProjectUserRepository>());

        var deptIds = await sut.GetDeptFilterIdsAsync(CancellationToken.None);

        Assert.NotNull(deptIds);
        Assert.Equal(3, deptIds!.Count);
        Assert.Contains(10L, deptIds);
        Assert.Contains(20L, deptIds);
        Assert.Contains(30L, deptIds);
    }

    [Fact]
    public async Task GetProjectFilterIdsAsync_WhenProjectScope_ShouldReturnProjectIdsForUser()
    {
        var user = new CurrentUserInfo(3, "u", "u", TestTenant, new[] { "Proj" }, false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var role = new Role(TestTenant, "项目", "Proj", 50);
        role.SetDataScope(DataScopeType.Project);

        var roleRepo = Substitute.For<IRoleRepository>();
        roleRepo.QueryByCodesAsync(TestTenant, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { role });

        var projectUserRepo = Substitute.For<IProjectUserRepository>();
        projectUserRepo.QueryProjectIdsByUserIdAsync(TestTenant, 3, Arg.Any<CancellationToken>())
            .Returns(new[] { 500L, 501L });

        var sut = new TenantDataScopeFilter(
            accessor,
            roleRepo,
            Substitute.For<IRoleDeptRepository>(),
            Substitute.For<IUserDepartmentRepository>(),
            Substitute.For<IDepartmentRepository>(),
            projectUserRepo);

        var projectIds = await sut.GetProjectFilterIdsAsync(CancellationToken.None);

        Assert.NotNull(projectIds);
        Assert.Equal(new[] { 500L, 501L }, projectIds!);
    }

    [Fact]
    public async Task GetEffectiveScopeAsync_WhenRoleHasAll_AndUserIsNotPlatformAdmin_ShouldDowngradeToCurrentTenant()
    {
        var user = new CurrentUserInfo(2, "u", "u", TestTenant, new[] { "AllRole" }, false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var role = new Role(TestTenant, "全部", "AllRole", 60);
        role.SetDataScope(DataScopeType.All);

        var roleRepo = Substitute.For<IRoleRepository>();
        roleRepo.QueryByCodesAsync(TestTenant, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { role });

        var sut = new TenantDataScopeFilter(
            accessor,
            roleRepo,
            Substitute.For<IRoleDeptRepository>(),
            Substitute.For<IUserDepartmentRepository>(),
            Substitute.For<IDepartmentRepository>(),
            Substitute.For<IProjectUserRepository>());

        var scope = await sut.GetEffectiveScopeAsync(CancellationToken.None);

        Assert.Equal(DataScopeType.CurrentTenant, scope);
    }
}
