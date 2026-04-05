using Atlas.Application.Platform.Repositories;
using Atlas.Core.Enums;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Services.Platform;
using NSubstitute;

namespace Atlas.SecurityPlatform.Tests.Platform;

public sealed class AppDataScopeFilterTests
{
    private static TenantId TestTenant => new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    private const long TestAppId = 100L;

    private static AppDataScopeFilter CreateSut(
        ICurrentUserAccessor? accessor = null,
        IAppUserRoleRepository? userRoleRepo = null,
        IAppRoleRepository? roleRepo = null,
        IAppMemberDepartmentRepository? memberDeptRepo = null,
        IAppDepartmentRepository? appDeptRepo = null,
        IAppProjectUserRepository? projectUserRepo = null)
    {
        return new AppDataScopeFilter(
            accessor ?? Substitute.For<ICurrentUserAccessor>(),
            userRoleRepo ?? Substitute.For<IAppUserRoleRepository>(),
            roleRepo ?? Substitute.For<IAppRoleRepository>(),
            memberDeptRepo ?? Substitute.For<IAppMemberDepartmentRepository>(),
            appDeptRepo ?? Substitute.For<IAppDepartmentRepository>(),
            projectUserRepo ?? Substitute.For<IAppProjectUserRepository>());
    }

    private static AppRole CreateAppRole(long id, DataScopeType scope, string? deptIds = null)
    {
        var role = new AppRole(
            TestTenant,
            TestAppId,
            $"code-{id}",
            $"name-{id}",
            null,
            false,
            1,
            DateTimeOffset.UtcNow,
            id);
        role.SetDataScope(scope, deptIds);
        return role;
    }

    [Fact]
    public async Task GetEffectiveScopeAsync_WhenCurrentUserIsNull_ReturnsOnlySelf()
    {
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns((CurrentUserInfo?)null);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        var roleRepo = Substitute.For<IAppRoleRepository>();

        var sut = CreateSut(accessor, userRoleRepo, roleRepo);

        var scope = await sut.GetEffectiveScopeAsync(TestAppId, CancellationToken.None);

        Assert.Equal(DataScopeType.OnlySelf, scope);
        await userRoleRepo.DidNotReceiveWithAnyArgs().QueryByUserIdsAsync(default!, default, default!, default);
    }

    [Fact]
    public async Task GetEffectiveScopeAsync_WhenNoAppUserRoles_ReturnsOnlySelf()
    {
        const long userId = 1L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, [], false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        userRoleRepo.QueryByUserIdsAsync(TestTenant, TestAppId, Arg.Is<long[]>(a => a.Length == 1 && a[0] == userId), Arg.Any<CancellationToken>())
            .Returns([]);

        var sut = CreateSut(accessor, userRoleRepo);

        var scope = await sut.GetEffectiveScopeAsync(TestAppId, CancellationToken.None);

        Assert.Equal(DataScopeType.OnlySelf, scope);
    }

    [Fact]
    public async Task GetEffectiveScopeAsync_WhenRoleRowsMissing_ReturnsOnlySelf()
    {
        const long userId = 2L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, [], false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        userRoleRepo.QueryByUserIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([new AppUserRole(TestTenant, TestAppId, userId, 10, 1)]);

        var roleRepo = Substitute.For<IAppRoleRepository>();
        roleRepo.QueryByIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var sut = CreateSut(accessor, userRoleRepo, roleRepo);

        var scope = await sut.GetEffectiveScopeAsync(TestAppId, CancellationToken.None);

        Assert.Equal(DataScopeType.OnlySelf, scope);
    }

    [Fact]
    public async Task GetEffectiveScopeAsync_WhenRoleHasAll_ReturnsAll()
    {
        const long userId = 3L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, [], false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        userRoleRepo.QueryByUserIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([new AppUserRole(TestTenant, TestAppId, userId, 20, 1)]);

        var roleRepo = Substitute.For<IAppRoleRepository>();
        roleRepo.QueryByIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([CreateAppRole(20, DataScopeType.All)]);

        var sut = CreateSut(accessor, userRoleRepo, roleRepo);

        var scope = await sut.GetEffectiveScopeAsync(TestAppId, CancellationToken.None);

        Assert.Equal(DataScopeType.All, scope);
    }

    [Fact]
    public async Task GetEffectiveScopeAsync_MultipleRoles_UsesMinimumEnumValue_MostPermissiveWins()
    {
        const long userId = 4L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, [], false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        userRoleRepo.QueryByUserIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                new AppUserRole(TestTenant, TestAppId, userId, 30, 1),
                new AppUserRole(TestTenant, TestAppId, userId, 31, 2)
            ]);

        var roleRepo = Substitute.For<IAppRoleRepository>();
        roleRepo.QueryByIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                CreateAppRole(30, DataScopeType.OnlySelf),
                CreateAppRole(31, DataScopeType.All)
            ]);

        var sut = CreateSut(accessor, userRoleRepo, roleRepo);

        var scope = await sut.GetEffectiveScopeAsync(TestAppId, CancellationToken.None);

        Assert.Equal(DataScopeType.All, scope);
    }

    [Fact]
    public async Task GetOwnerFilterIdAsync_WhenEffectiveScopeIsOnlySelf_ReturnsCurrentUserId()
    {
        const long userId = 42L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, [], false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        userRoleRepo.QueryByUserIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([new AppUserRole(TestTenant, TestAppId, userId, 40, 1)]);

        var roleRepo = Substitute.For<IAppRoleRepository>();
        roleRepo.QueryByIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([CreateAppRole(40, DataScopeType.OnlySelf)]);

        var sut = CreateSut(accessor, userRoleRepo, roleRepo);

        var ownerId = await sut.GetOwnerFilterIdAsync(TestAppId, CancellationToken.None);

        Assert.Equal(userId, ownerId);
    }

    [Fact]
    public async Task GetOwnerFilterIdAsync_WhenEffectiveScopeIsAll_ReturnsNull()
    {
        const long userId = 43L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, [], false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        userRoleRepo.QueryByUserIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([new AppUserRole(TestTenant, TestAppId, userId, 50, 1)]);

        var roleRepo = Substitute.For<IAppRoleRepository>();
        roleRepo.QueryByIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([CreateAppRole(50, DataScopeType.All)]);

        var sut = CreateSut(accessor, userRoleRepo, roleRepo);

        var ownerId = await sut.GetOwnerFilterIdAsync(TestAppId, CancellationToken.None);

        Assert.Null(ownerId);
    }

    [Fact]
    public async Task GetDeptFilterIdsAsync_WhenCustomDept_ReturnsDeptIdsFromRoleDeptIdsString()
    {
        const long userId = 7L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, [], false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        userRoleRepo.QueryByUserIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([new AppUserRole(TestTenant, TestAppId, userId, 60, 1)]);

        var roleRepo = Substitute.For<IAppRoleRepository>();
        roleRepo.QueryByIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([CreateAppRole(60, DataScopeType.CustomDept, "100, 200 ,100")]);

        var sut = CreateSut(accessor, userRoleRepo, roleRepo);

        var deptIds = await sut.GetDeptFilterIdsAsync(TestAppId, CancellationToken.None);

        Assert.NotNull(deptIds);
        Assert.Equal(2, deptIds!.Count);
        Assert.Contains(100L, deptIds);
        Assert.Contains(200L, deptIds);
    }

    [Fact]
    public async Task GetDeptFilterIdsAsync_WhenCurrentDept_ReturnsAppMemberDepartmentIds()
    {
        const long userId = 8L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, [], false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        userRoleRepo.QueryByUserIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([new AppUserRole(TestTenant, TestAppId, userId, 70, 1)]);

        var roleRepo = Substitute.For<IAppRoleRepository>();
        roleRepo.QueryByIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([CreateAppRole(70, DataScopeType.CurrentDept)]);

        var memberDeptRepo = Substitute.For<IAppMemberDepartmentRepository>();
        memberDeptRepo.QueryByUserIdAsync(TestTenant, TestAppId, userId, Arg.Any<CancellationToken>())
            .Returns(
            [
                new AppMemberDepartment(TestTenant, TestAppId, userId, 10, true, 1),
                new AppMemberDepartment(TestTenant, TestAppId, userId, 11, false, 2)
            ]);

        var sut = CreateSut(accessor, userRoleRepo, roleRepo, memberDeptRepo);

        var deptIds = await sut.GetDeptFilterIdsAsync(TestAppId, CancellationToken.None);

        Assert.NotNull(deptIds);
        Assert.Equal(2, deptIds!.Count);
        Assert.Contains(10L, deptIds);
        Assert.Contains(11L, deptIds);
    }

    [Fact]
    public async Task GetDeptFilterIdsAsync_WhenCurrentDeptAndBelow_IncludesDescendantsFromAppDepartmentTree()
    {
        const long userId = 9L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, [], false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        userRoleRepo.QueryByUserIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([new AppUserRole(TestTenant, TestAppId, userId, 80, 1)]);

        var roleRepo = Substitute.For<IAppRoleRepository>();
        roleRepo.QueryByIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([CreateAppRole(80, DataScopeType.CurrentDeptAndBelow)]);

        var memberDeptRepo = Substitute.For<IAppMemberDepartmentRepository>();
        memberDeptRepo.QueryByUserIdAsync(TestTenant, TestAppId, userId, Arg.Any<CancellationToken>())
            .Returns([new AppMemberDepartment(TestTenant, TestAppId, userId, 10, true, 1)]);

        var root = new AppDepartment(TestTenant, TestAppId, "总部", "HQ", null, 0, 10);
        var child = new AppDepartment(TestTenant, TestAppId, "分部", "BR", 10, 1, 20);
        var grandChild = new AppDepartment(TestTenant, TestAppId, "小组", "TM", 20, 2, 30);

        var appDeptRepo = Substitute.For<IAppDepartmentRepository>();
        appDeptRepo.QueryByAppIdAsync(TestTenant, TestAppId, Arg.Any<CancellationToken>())
            .Returns([root, child, grandChild]);

        var sut = CreateSut(accessor, userRoleRepo, roleRepo, memberDeptRepo, appDeptRepo);

        var deptIds = await sut.GetDeptFilterIdsAsync(TestAppId, CancellationToken.None);

        Assert.NotNull(deptIds);
        Assert.Equal(3, deptIds!.Count);
        Assert.Contains(10L, deptIds);
        Assert.Contains(20L, deptIds);
        Assert.Contains(30L, deptIds);
    }

    [Fact]
    public async Task GetDeptFilterIdsAsync_WhenAll_ReturnsNull()
    {
        const long userId = 10L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, [], false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        userRoleRepo.QueryByUserIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([new AppUserRole(TestTenant, TestAppId, userId, 90, 1)]);

        var roleRepo = Substitute.For<IAppRoleRepository>();
        roleRepo.QueryByIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([CreateAppRole(90, DataScopeType.All)]);

        var sut = CreateSut(accessor, userRoleRepo, roleRepo);

        var deptIds = await sut.GetDeptFilterIdsAsync(TestAppId, CancellationToken.None);

        Assert.Null(deptIds);
    }

    [Fact]
    public async Task GetDeptFilterIdsAsync_WhenCurrentTenant_ReturnsNull()
    {
        const long userId = 11L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, [], false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        userRoleRepo.QueryByUserIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([new AppUserRole(TestTenant, TestAppId, userId, 91, 1)]);

        var roleRepo = Substitute.For<IAppRoleRepository>();
        roleRepo.QueryByIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([CreateAppRole(91, DataScopeType.CurrentTenant)]);

        var sut = CreateSut(accessor, userRoleRepo, roleRepo);

        var deptIds = await sut.GetDeptFilterIdsAsync(TestAppId, CancellationToken.None);

        Assert.Null(deptIds);
    }

    [Fact]
    public async Task GetProjectFilterIdsAsync_WhenProjectScope_ReturnsDistinctProjectIdsForUser()
    {
        const long userId = 12L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, [], false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        userRoleRepo.QueryByUserIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([new AppUserRole(TestTenant, TestAppId, userId, 100, 1)]);

        var roleRepo = Substitute.For<IAppRoleRepository>();
        roleRepo.QueryByIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([CreateAppRole(100, DataScopeType.Project)]);

        var projectUserRepo = Substitute.For<IAppProjectUserRepository>();
        projectUserRepo.QueryByUserIdAsync(TestTenant, TestAppId, userId, Arg.Any<CancellationToken>())
            .Returns(
            [
                new AppProjectUser(TestTenant, TestAppId, 500, userId, 1),
                new AppProjectUser(TestTenant, TestAppId, 501, userId, 2),
                new AppProjectUser(TestTenant, TestAppId, 500, userId, 3)
            ]);

        var sut = CreateSut(accessor, userRoleRepo, roleRepo, projectUserRepo: projectUserRepo);

        var projectIds = await sut.GetProjectFilterIdsAsync(TestAppId, CancellationToken.None);

        Assert.NotNull(projectIds);
        Assert.Equal(2, projectIds!.Count);
        Assert.Contains(500L, projectIds);
        Assert.Contains(501L, projectIds);
    }

    [Fact]
    public async Task GetProjectFilterIdsAsync_WhenAllScope_ReturnsNull()
    {
        const long userId = 13L;
        var user = new CurrentUserInfo(userId, "u", "u", TestTenant, [], false);
        var accessor = Substitute.For<ICurrentUserAccessor>();
        accessor.GetCurrentUser().Returns(user);

        var userRoleRepo = Substitute.For<IAppUserRoleRepository>();
        userRoleRepo.QueryByUserIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([new AppUserRole(TestTenant, TestAppId, userId, 110, 1)]);

        var roleRepo = Substitute.For<IAppRoleRepository>();
        roleRepo.QueryByIdsAsync(TestTenant, TestAppId, Arg.Any<IReadOnlyList<long>>(), Arg.Any<CancellationToken>())
            .Returns([CreateAppRole(110, DataScopeType.All)]);

        var sut = CreateSut(accessor, userRoleRepo, roleRepo);

        var projectIds = await sut.GetProjectFilterIdsAsync(TestAppId, CancellationToken.None);

        Assert.Null(projectIds);
    }
}
