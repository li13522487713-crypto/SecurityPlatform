using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.Identity.Entities;

public class UserAccount : TenantEntity
{
    public UserAccount()
        : base(TenantId.Empty)
    {
        Username = string.Empty;
        DisplayName = string.Empty;
        PasswordHash = string.Empty;
        Roles = string.Empty;
        Email = string.Empty;
        PhoneNumber = string.Empty;
        IsActive = false;
        IsSystem = false;
        FailedLoginCount = 0;
        LockoutEndAt = DateTimeOffset.MinValue;
        ManualLockAt = DateTimeOffset.MinValue;
        LastPasswordChangeAt = DateTimeOffset.UtcNow;
        LastLoginAt = DateTimeOffset.MinValue;
    }

    public UserAccount(TenantId tenantId, string username, string passwordHash, string roles)
        : base(tenantId)
    {
        Username = username;
        DisplayName = username;
        PasswordHash = passwordHash;
        Roles = roles;
        Email = string.Empty;
        PhoneNumber = string.Empty;
        IsActive = true;
        IsSystem = false;
        FailedLoginCount = 0;
        LockoutEndAt = DateTimeOffset.MinValue;
        ManualLockAt = DateTimeOffset.MinValue;
        LastPasswordChangeAt = DateTimeOffset.UtcNow;
        LastLoginAt = DateTimeOffset.MinValue;
    }

    public UserAccount(TenantId tenantId, string username, string displayName, string passwordHash, long id)
        : base(tenantId)
    {
        Id = id;
        Username = username;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        Roles = string.Empty;
        Email = string.Empty;
        PhoneNumber = string.Empty;
        IsActive = true;
        IsSystem = false;
        FailedLoginCount = 0;
        LockoutEndAt = DateTimeOffset.MinValue;
        ManualLockAt = DateTimeOffset.MinValue;
        LastPasswordChangeAt = DateTimeOffset.UtcNow;
        LastLoginAt = DateTimeOffset.MinValue;
    }

    public string Username { get; private set; }
    public string DisplayName { get; private set; }
    public string PasswordHash { get; private set; }
    public string Roles { get; private set; }
    public string? Email { get; private set; }
    public string? PhoneNumber { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsSystem { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTimeOffset LockoutEndAt { get; private set; }
    public bool IsManualLocked { get; private set; }
    public DateTimeOffset ManualLockAt { get; private set; }
    public DateTimeOffset LastPasswordChangeAt { get; private set; }
    public DateTimeOffset LastLoginAt { get; private set; }

    public void UpdatePassword(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        LastPasswordChangeAt = now;
        FailedLoginCount = 0;
        LockoutEndAt = DateTimeOffset.MinValue;
        IsManualLocked = false;
        ManualLockAt = DateTimeOffset.MinValue;
    }

    public void MarkLoginSuccess(DateTimeOffset now)
    {
        FailedLoginCount = 0;
        LockoutEndAt = DateTimeOffset.MinValue;
        IsManualLocked = false;
        ManualLockAt = DateTimeOffset.MinValue;
        LastLoginAt = now;
    }

    public void MarkLoginFailure(DateTimeOffset now, int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginCount += 1;
        if (FailedLoginCount >= maxFailedAttempts)
        {
            LockoutEndAt = now.Add(lockoutDuration);
        }
    }

    public void ManualLock(DateTimeOffset now)
    {
        IsManualLocked = true;
        ManualLockAt = now;
    }

    public void Unlock()
    {
        FailedLoginCount = 0;
        LockoutEndAt = DateTimeOffset.MinValue;
        IsManualLocked = false;
        ManualLockAt = DateTimeOffset.MinValue;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void UpdateProfile(string displayName, string? email, string? phoneNumber)
    {
        DisplayName = displayName;
        Email = email ?? string.Empty;
        PhoneNumber = phoneNumber ?? string.Empty;
    }

    public void UpdateRoles(string roles)
    {
        Roles = roles;
    }

    public void MarkSystemAccount()
    {
        IsSystem = true;
    }
}
