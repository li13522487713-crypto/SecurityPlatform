using Atlas.Application.Audit.Abstractions;
using Atlas.Application.LowCode.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Atlas.SecurityPlatform.Tests.LowCode;

/// <summary>
/// P0-5 守门测试：<see cref="RuntimeWebviewDomainService.IsAllowedAsync"/> 白名单匹配语义。
///
/// 此前 dispatch 处理 open_external_link 仅前端校验，可被绕过；
/// 现要求服务端必须对 URL 做：
///  1) Uri 合法性 + 仅 http(s)；
///  2) host 必须命中已 verified 的 LowCodeWebviewDomain（精确 OR 子域名匹配）；
///  3) 未 verified 的域名一律拒绝（哪怕 domain 字段相等）。
/// </summary>
public sealed class RuntimeWebviewDomainAllowListTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    private static RuntimeWebviewDomainService CreateService(IReadOnlyList<LowCodeWebviewDomain> seed)
    {
        var repo = Substitute.For<ILowCodeWebviewDomainRepository>();
        repo.ListAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(seed));
        var idGen = Substitute.For<IIdGeneratorAccessor>();
        var audit = Substitute.For<IAuditWriter>();
        var httpFactory = Substitute.For<IHttpClientFactory>();
        return new RuntimeWebviewDomainService(repo, idGen, audit, httpFactory, NullLogger<RuntimeWebviewDomainService>.Instance);
    }

    private static LowCodeWebviewDomain Verified(string domain, string kind = "http_file")
    {
        var d = new LowCodeWebviewDomain(Tenant, 1L, domain, kind, "tok-xyz", createdByUserId: 0L);
        d.MarkVerified();
        return d;
    }

    private static LowCodeWebviewDomain Unverified(string domain, string kind = "dns_txt")
        => new(Tenant, 1L, domain, kind, "tok-xyz", createdByUserId: 0L);

    [Fact]
    public async Task Empty_Or_Invalid_Url_Is_Rejected()
    {
        var svc = CreateService([Verified("example.com")]);
        Assert.False(await svc.IsAllowedAsync(Tenant, "", default));
        Assert.False(await svc.IsAllowedAsync(Tenant, "   ", default));
        Assert.False(await svc.IsAllowedAsync(Tenant, "not a url", default));
    }

    [Fact]
    public async Task NonHttp_Scheme_Is_Rejected()
    {
        var svc = CreateService([Verified("example.com")]);
        Assert.False(await svc.IsAllowedAsync(Tenant, "javascript:alert(1)", default));
        Assert.False(await svc.IsAllowedAsync(Tenant, "data:text/html,xx", default));
        Assert.False(await svc.IsAllowedAsync(Tenant, "file:///etc/passwd", default));
        Assert.False(await svc.IsAllowedAsync(Tenant, "ftp://example.com/x", default));
    }

    [Fact]
    public async Task Exact_Host_Match_On_Verified_Domain_Is_Allowed()
    {
        var svc = CreateService([Verified("example.com")]);
        Assert.True(await svc.IsAllowedAsync(Tenant, "https://example.com/app", default));
        Assert.True(await svc.IsAllowedAsync(Tenant, "http://example.com", default));
        // 大小写不敏感
        Assert.True(await svc.IsAllowedAsync(Tenant, "https://EXAMPLE.COM/x", default));
    }

    [Fact]
    public async Task Subdomain_Of_Verified_Domain_Is_Allowed()
    {
        var svc = CreateService([Verified("example.com")]);
        Assert.True(await svc.IsAllowedAsync(Tenant, "https://a.example.com/x", default));
        Assert.True(await svc.IsAllowedAsync(Tenant, "https://b.c.example.com/y", default));
    }

    [Fact]
    public async Task Sibling_Or_Parent_Domain_Is_NOT_Allowed()
    {
        var svc = CreateService([Verified("foo.example.com")]);
        // 父域 example.com 不在白名单 → 拒绝
        Assert.False(await svc.IsAllowedAsync(Tenant, "https://example.com/x", default));
        // 同级域 bar.example.com 不命中 foo.example.com 的子域规则 → 拒绝
        Assert.False(await svc.IsAllowedAsync(Tenant, "https://bar.example.com/x", default));
        // 看似前缀但非子域 → 拒绝（防 evilfoo.example.com / fooexample.com 类绕过）
        Assert.False(await svc.IsAllowedAsync(Tenant, "https://fooexample.com/x", default));
    }

    [Fact]
    public async Task Unverified_Domain_Is_Rejected_Even_If_Listed()
    {
        // 即使 domain 字段相等，未 verified 的也不允许；防止 dns_txt:not-implemented 类历史漏洞
        var svc = CreateService([Unverified("example.com")]);
        Assert.False(await svc.IsAllowedAsync(Tenant, "https://example.com/x", default));
    }

    [Fact]
    public async Task Empty_Allow_List_Rejects_Everything()
    {
        var svc = CreateService(Array.Empty<LowCodeWebviewDomain>());
        Assert.False(await svc.IsAllowedAsync(Tenant, "https://example.com/", default));
        Assert.False(await svc.IsAllowedAsync(Tenant, "https://github.com/", default));
    }

    [Fact]
    public async Task Multiple_Verified_Domains_Each_Allowed_Independently()
    {
        var svc = CreateService(
        [
            Verified("example.com"),
            Verified("trusted.io"),
            Unverified("untrusted.org") // 未 verified 应继续被拒
        ]);
        Assert.True(await svc.IsAllowedAsync(Tenant, "https://example.com/", default));
        Assert.True(await svc.IsAllowedAsync(Tenant, "https://api.trusted.io/v1", default));
        Assert.False(await svc.IsAllowedAsync(Tenant, "https://untrusted.org/", default));
    }
}
