using Atlas.Application.Abstractions;
using Atlas.Application.Identity;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace Atlas.SecurityPlatform.Tests.Platform;

public sealed class PermissionDecisionServiceTests
{
    private static TenantId TestTenant => new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    [Fact]
    public async Task HasPermissionAsync_WhenUserIsPlatformAdmin_ShouldReturnTrueForArbitraryPermission()
    {
        var sut = CreateSut(out var userRepo, out var rbac);
        var userId = 101L;
        var account = new UserAccount(TestTenant, "u", "d", "hash", userId);
        account.MarkPlatformAdmin();
        userRepo.FindByIdAsync(TestTenant, userId, Arg.Any<CancellationToken>()).Returns(account);
        rbac.GetRolesAndPermissionsAsync(account, TestTenant, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<string>(), Array.Empty<string>()));

        var allowed = await sut.HasPermissionAsync(TestTenant, userId, "nonexistent:permission", CancellationToken.None);

        Assert.True(allowed);
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserHasSystemAdminPermission_ShouldReturnTrueForArbitraryPermission()
    {
        var sut = CreateSut(out var userRepo, out var rbac);
        var userId = 102L;
        var account = new UserAccount(TestTenant, "u", "d", "hash", userId);
        userRepo.FindByIdAsync(TestTenant, userId, Arg.Any<CancellationToken>()).Returns(account);
        rbac.GetRolesAndPermissionsAsync(account, TestTenant, Arg.Any<CancellationToken>())
            .Returns((Array.Empty<string>(), new[] { PermissionCodes.SystemAdmin }));

        var allowed = await sut.HasPermissionAsync(TestTenant, userId, "any:code", CancellationToken.None);

        Assert.True(allowed);
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserHasNoMatchingPermission_ShouldReturnFalse()
    {
        var sut = CreateSut(out var userRepo, out var rbac);
        var userId = 103L;
        var account = new UserAccount(TestTenant, "u", "d", "hash", userId);
        userRepo.FindByIdAsync(TestTenant, userId, Arg.Any<CancellationToken>()).Returns(account);
        rbac.GetRolesAndPermissionsAsync(account, TestTenant, Arg.Any<CancellationToken>())
            .Returns((new[] { "Member" }, Array.Empty<string>()));

        var allowed = await sut.HasPermissionAsync(TestTenant, userId, PermissionCodes.UsersView, CancellationToken.None);

        Assert.False(allowed);
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserHasSpecificPermission_ShouldReturnTrue()
    {
        var sut = CreateSut(out var userRepo, out var rbac);
        var userId = 104L;
        var account = new UserAccount(TestTenant, "u", "d", "hash", userId);
        userRepo.FindByIdAsync(TestTenant, userId, Arg.Any<CancellationToken>()).Returns(account);
        rbac.GetRolesAndPermissionsAsync(account, TestTenant, Arg.Any<CancellationToken>())
            .Returns((new[] { "Operator" }, new[] { PermissionCodes.UsersView }));

        var allowed = await sut.HasPermissionAsync(TestTenant, userId, PermissionCodes.UsersView, CancellationToken.None);

        Assert.True(allowed);
    }

    private static PermissionDecisionService CreateSut(
        out IUserAccountRepository userRepo,
        out IRbacResolver rbac)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        userRepo = Substitute.For<IUserAccountRepository>();
        var userRoleRepo = Substitute.For<IUserRoleRepository>();
        rbac = Substitute.For<IRbacResolver>();
        return new PermissionDecisionService(cache, userRepo, userRoleRepo, rbac);
    }
}
