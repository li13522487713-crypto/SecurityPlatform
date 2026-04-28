using Atlas.Core.Tenancy;
using Atlas.Infrastructure.ExternalConnectors.HostedServices;
using Atlas.Presentation.Shared.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Atlas.AppHost.ExternalConnectors.Bridges;

/// <summary>
/// 给 Hangfire job 用的 TenantContextWriter。
/// Hangfire job 没有 HttpContext，这里通过 IHttpContextAccessor 显式构造一个临时 HttpContext.Items 容器，
/// 让现有的 HttpContextTenantProvider 能读到 TenantId。
/// AppHost 保留同一桥接以满足 ConnectorRecurringJobScheduler 等场景的 DI 完整性。
/// </summary>
public sealed class ConnectorTenantContextWriterBridge : ITenantContextWriter
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ConnectorTenantContextWriterBridge(IHttpContextAccessor httpContextAccessor)
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
