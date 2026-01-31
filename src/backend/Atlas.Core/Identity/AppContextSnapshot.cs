using Atlas.Core.Tenancy;

namespace Atlas.Core.Identity;

public sealed record AppContextSnapshot(
    TenantId TenantId,
    string AppId,
    CurrentUserInfo? CurrentUser,
    ClientContext ClientContext,
    string? TraceId) : IAppContext;
