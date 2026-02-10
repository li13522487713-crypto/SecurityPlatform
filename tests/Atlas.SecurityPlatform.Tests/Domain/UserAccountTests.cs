using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;

namespace Atlas.SecurityPlatform.Tests.Domain;

public sealed class UserAccountTests
{
    private static readonly TenantId TestTenant = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    [Fact]
    public void Constructor_CreatesActiveUser()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test User", "hash123", 1L);
        Assert.Equal("testuser", user.Username);
        Assert.Equal("Test User", user.DisplayName);
        Assert.True(user.IsActive);
        Assert.False(user.IsSystem);
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public void UpdateProfile_ChangesFields()
    {
        var user = new UserAccount(TestTenant, "testuser", "Old Name", "hash", 1L);
        user.UpdateProfile("New Name", "email@test.com", "1234567890");
        Assert.Equal("New Name", user.DisplayName);
        Assert.Equal("email@test.com", user.Email);
        Assert.Equal("1234567890", user.PhoneNumber);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "hash", 1L);
        Assert.True(user.IsActive);
        user.Deactivate();
        Assert.False(user.IsActive);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "hash", 1L);
        user.Deactivate();
        user.Activate();
        Assert.True(user.IsActive);
    }

    [Fact]
    public void MarkLoginFailure_IncrementsCount()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "hash", 1L);
        var now = DateTimeOffset.UtcNow;
        user.MarkLoginFailure(now, 5, TimeSpan.FromMinutes(15));
        Assert.Equal(1, user.FailedLoginCount);
    }

    [Fact]
    public void MarkLoginFailure_AtMaxAttempts_TriggersLockout()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "hash", 1L);
        var now = DateTimeOffset.UtcNow;
        var lockoutDuration = TimeSpan.FromMinutes(15);
        var maxAttempts = 5;

        for (var i = 0; i < maxAttempts; i++)
        {
            user.MarkLoginFailure(now, maxAttempts, lockoutDuration);
        }

        Assert.Equal(maxAttempts, user.FailedLoginCount);
        Assert.True(user.LockoutEndAt > now);
    }

    [Fact]
    public void MarkLoginSuccess_ResetsFailedCount()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "hash", 1L);
        var now = DateTimeOffset.UtcNow;
        user.MarkLoginFailure(now, 5, TimeSpan.FromMinutes(15));
        user.MarkLoginFailure(now, 5, TimeSpan.FromMinutes(15));
        Assert.Equal(2, user.FailedLoginCount);

        user.MarkLoginSuccess(now);
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public void ManualLock_SetsIsManualLockedTrue()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "hash", 1L);
        var now = DateTimeOffset.UtcNow;
        user.ManualLock(now);
        Assert.True(user.IsManualLocked);
    }

    [Fact]
    public void Unlock_ClearsAllLockState()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "hash", 1L);
        var now = DateTimeOffset.UtcNow;
        user.ManualLock(now);
        user.MarkLoginFailure(now, 5, TimeSpan.FromMinutes(15));

        user.Unlock();
        Assert.False(user.IsManualLocked);
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public void UpdatePassword_ResetsFailedCountAndChangesHash()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "oldhash", 1L);
        var now = DateTimeOffset.UtcNow;
        user.MarkLoginFailure(now, 5, TimeSpan.FromMinutes(15));

        user.UpdatePassword("newhash", now);
        Assert.Equal("newhash", user.PasswordHash);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.False(user.IsManualLocked);
    }

    [Fact]
    public void UpdateRoles_ChangesRolesString()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "hash", 1L);
        user.UpdateRoles("admin,editor");
        Assert.Equal("admin,editor", user.Roles);
    }

    [Fact]
    public void SetupMfa_StoresSecretKey()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "hash", 1L);
        user.SetupMfa("JBSWY3DPEHPK3PXP");
        Assert.Equal("JBSWY3DPEHPK3PXP", user.MfaSecretKey);
        Assert.False(user.MfaEnabled);
    }

    [Fact]
    public void EnableMfa_SetsMfaEnabledTrue()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "hash", 1L);
        user.SetupMfa("secret");
        user.EnableMfa();
        Assert.True(user.MfaEnabled);
    }

    [Fact]
    public void DisableMfa_ClearsSecretAndDisables()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "hash", 1L);
        user.SetupMfa("secret");
        user.EnableMfa();
        user.DisableMfa();
        Assert.False(user.MfaEnabled);
        Assert.Null(user.MfaSecretKey);
    }

    [Fact]
    public void MarkSystemAccount_SetsIsSystemTrue()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "hash", 1L);
        user.MarkSystemAccount();
        Assert.True(user.IsSystem);
    }

    [Fact]
    public void TenantId_MatchesConstructorValue()
    {
        var user = new UserAccount(TestTenant, "testuser", "Test", "hash", 1L);
        Assert.Equal(TestTenant.Value, user.TenantIdValue);
        Assert.Equal(TestTenant, user.TenantId);
    }
}
