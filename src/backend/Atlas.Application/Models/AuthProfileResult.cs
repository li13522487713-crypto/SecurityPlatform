using Atlas.Core.Identity;

namespace Atlas.Application.Models;

public sealed record AuthProfileResult(
    string Id,
    string Username,
    string DisplayName,
    string TenantId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool IsPlatformAdmin,
    ClientContextView? ClientContext);
