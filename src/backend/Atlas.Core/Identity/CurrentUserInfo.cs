using Atlas.Core.Tenancy;

namespace Atlas.Core.Identity;

public sealed record CurrentUserInfo(
    long UserId,
    string Username,
    string DisplayName,
    TenantId TenantId,
    IReadOnlyList<string> Roles,
    bool IsPlatformAdmin = false,
    long SessionId = 0);
