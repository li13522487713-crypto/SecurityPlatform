using Atlas.Core.Tenancy;

namespace Atlas.Core.Identity;

public interface IAppContext
{
    TenantId TenantId { get; }
    string AppId { get; }
    CurrentUserInfo? CurrentUser { get; }
    ClientContext ClientContext { get; }
    string? TraceId { get; }
}
