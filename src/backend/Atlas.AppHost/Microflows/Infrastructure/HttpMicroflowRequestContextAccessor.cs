using System.Security.Claims;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Infrastructure;

namespace Atlas.AppHost.Microflows.Infrastructure;

public sealed class HttpMicroflowRequestContextAccessor : IMicroflowRequestContextAccessor
{
    private const string WorkspaceHeaderName = "X-Workspace-Id";
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string UserHeaderName = "X-User-Id";
    private const string LocaleHeaderName = "X-Locale";
    private const string TraceHeaderName = "X-Trace-Id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpMicroflowRequestContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public MicroflowRequestContext Current
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return new MicroflowRequestContext
                {
                    TraceId = Guid.NewGuid().ToString("N")
                };
            }

            var user = httpContext.User;
            var traceId = ReadHeader(httpContext, TraceHeaderName)
                ?? httpContext.TraceIdentifier
                ?? Guid.NewGuid().ToString("N");

            return new MicroflowRequestContext
            {
                WorkspaceId = ReadHeader(httpContext, WorkspaceHeaderName),
                TenantId = ReadHeader(httpContext, TenantHeaderName) ?? ReadClaim(user, "tenant_id"),
                UserId = ReadHeader(httpContext, UserHeaderName)
                    ?? ReadClaim(user, "user_id")
                    ?? ReadClaim(user, ClaimTypes.NameIdentifier)
                    ?? ReadClaim(user, "sub"),
                UserName = ReadClaim(user, ClaimTypes.Name) ?? ReadClaim(user, "name"),
                Roles = user.FindAll(ClaimTypes.Role).Select(static claim => claim.Value).Distinct(StringComparer.Ordinal).ToArray(),
                Locale = ReadHeader(httpContext, LocaleHeaderName)
                    ?? httpContext.Request.Headers.AcceptLanguage.FirstOrDefault(),
                TraceId = traceId
            };
        }
    }

    private static string? ReadHeader(HttpContext httpContext, string headerName)
    {
        var value = httpContext.Request.Headers[headerName].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? ReadClaim(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
