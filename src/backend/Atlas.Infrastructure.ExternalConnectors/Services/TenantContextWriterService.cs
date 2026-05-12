using Atlas.Core.Tenancy;
using Atlas.Infrastructure.ExternalConnectors.HostedServices;
using Microsoft.AspNetCore.Http;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

public sealed class TenantContextWriterService : ITenantContextWriter
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContextWriterService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public IDisposable BeginScope(IServiceProvider scopeServices, TenantId tenantId)
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is null)
        {
            var fake = new DefaultHttpContext();
            fake.RequestServices = scopeServices;
            fake.Items["Atlas.TenantContext"] = new TenantContext(tenantId);
            _httpContextAccessor.HttpContext = fake;
            return new Disposer(() => _httpContextAccessor.HttpContext = null);
        }

        var previous = ctx.Items.ContainsKey("Atlas.TenantContext") ? ctx.Items["Atlas.TenantContext"] : null;
        ctx.Items["Atlas.TenantContext"] = new TenantContext(tenantId);
        return new Disposer(() =>
        {
            if (previous is null)
            {
                ctx.Items.Remove("Atlas.TenantContext");
            }
            else
            {
                ctx.Items["Atlas.TenantContext"] = previous;
            }
        });
    }

    private sealed class Disposer : IDisposable
    {
        private readonly Action _dispose;
        public Disposer(Action dispose) { _dispose = dispose; }
        public void Dispose() => _dispose();
    }
}
