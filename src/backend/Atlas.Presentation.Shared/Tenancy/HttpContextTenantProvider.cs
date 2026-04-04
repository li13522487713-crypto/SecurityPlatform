using Atlas.Core.Tenancy;

namespace Atlas.Presentation.Shared.Tenancy;

public sealed class HttpContextTenantProvider : ITenantProvider
{
    public const string TenantContextKey = "Atlas.TenantContext";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextTenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public TenantId GetTenantId()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return TenantId.Empty;
        }

        if (context.Items.TryGetValue(TenantContextKey, out var value) && value is TenantContext tenantContext)
        {
            return tenantContext.TenantId;
        }

        return TenantId.Empty;
    }
}