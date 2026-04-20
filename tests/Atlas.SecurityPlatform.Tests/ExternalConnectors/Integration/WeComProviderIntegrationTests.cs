using System.Net.Http;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Caching;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.WeCom;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.ExternalConnectors.Integration;

/// <summary>
/// 通过 WireMock.Net 桩 WeCom OpenAPI，验证 N1 引入 Senparc + 修复后的:
/// - access_token 缓存与 gettoken 链路；
/// - getapprovaldetail / getapprovalinfo 双链路（N1 补齐）；
/// - applyevent 提单 → sp_no；
/// - 60011 / 60020 应用可见范围错误自动降级为空集合。
/// </summary>
public sealed class WeComProviderIntegrationTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _httpClient;
    private readonly WeComApiClient _apiClient;
    private readonly WeComOptions _options;
    private readonly InMemoryConnectorTokenCache _tokenCache;
    private readonly WeComRuntimeOptions _runtime;

    public WeComProviderIntegrationTests()
    {
        _server = WireMockServer.Start();

        _options = new WeComOptions
        {
            ApiBaseUrl = _server.Url!,
            OAuthBaseUrl = _server.Url!,
        };

        _httpClient = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(WeComApiClient.HttpClientName).Returns(_ => new HttpClient { BaseAddress = new Uri(_server.Url!) });

        _tokenCache = new InMemoryConnectorTokenCache(new MemoryCache(new MemoryCacheOptions()));
        _runtime = new WeComRuntimeOptions
        {
            CorpId = "wxCorpTest",
            CorpSecret = "secretTest",
            AgentId = "1000003",
            CallbackBaseUrl = $"{_server.Url}/cb",
            TrustedDomains = new[] { "platform.example.com" },
        };

        _apiClient = new WeComApiClient(httpFactory, _tokenCache, Options.Create(_options), NullLogger<WeComApiClient>.Instance);
    }

    private ConnectorContext NewContext() => new()
    {
        TenantId = Guid.NewGuid(),
        ProviderInstanceId = 1,
        ProviderType = WeComConnectorMarker.ProviderType,
        RuntimeOptions = _runtime,
    };

    [Fact]
    public async Task GetAccessToken_HitsGetTokenEndpoint_AndCachesValue()
    {
        _server.Given(Request.Create().WithPath("/cgi-bin/gettoken").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                errcode = 0,
                errmsg = "ok",
                access_token = "real-token-1",
                expires_in = 7200,
            }));

        var ctx = NewContext();
        var t1 = await _apiClient.GetAccessTokenAsync(ctx, CancellationToken.None);
        var t2 = await _apiClient.GetAccessTokenAsync(ctx, CancellationToken.None);

        Assert.Equal("real-token-1", t1);
        Assert.Equal(t1, t2);
        // 第二次应命中缓存，不再触发 gettoken。
        var hitCount = _server.LogEntries.Count(e => e.RequestMessage.Path.Contains("/cgi-bin/gettoken"));
        Assert.Equal(1, hitCount);
    }

    [Fact]
    public async Task SubmitApproval_PostsApplyEvent_AndParsesSpNo()
    {
        StubAccessToken();
        _server.Given(Request.Create().WithPath("/cgi-bin/oa/applyevent").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                errcode = 0,
                errmsg = "ok",
                sp_no = "202604180001",
            }));

        var provider = new WeComApprovalProvider(_apiClient);
        var submission = new Atlas.Connectors.Core.Models.ExternalApprovalSubmission
        {
            ApplicantExternalUserId = "zhangsan",
            ExternalTemplateId = "tpl-leave",
            BusinessKey = "approval-12345",
            Fields = new Dictionary<string, Atlas.Connectors.Core.Models.ExternalApprovalFieldValue>
            {
                ["days"] = new() { ValueType = "number", RawJson = "3" },
            },
        };
        var ctx = NewContext();
        var result = await provider.SubmitApprovalAsync(ctx, submission, CancellationToken.None);

        Assert.Equal("202604180001", result.ExternalInstanceId);
        Assert.Equal(Atlas.Connectors.Core.Models.ExternalApprovalStatus.Pending, result.Status);
    }

    [Fact]
    public async Task ListRecentInstanceIds_HitsGetApprovalInfo_AndReturnsSpNoList()
    {
        StubAccessToken();
        _server.Given(Request.Create().WithPath("/cgi-bin/oa/getapprovalinfo").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                errcode = 0,
                errmsg = "ok",
                sp_no_list = new[] { "202604180001", "202604180002", "202604180003" },
                next_cursor = "cursor-002",
            }));

        var provider = new WeComApprovalProvider(_apiClient);
        var ctx = NewContext();
        var page = await provider.ListRecentInstanceIdsAsync(ctx, new ExternalApprovalInstanceIdQuery
        {
            StartTime = DateTimeOffset.UtcNow.AddDays(-1),
            EndTime = DateTimeOffset.UtcNow,
            TemplateId = "tpl-leave",
            Size = 10,
        }, CancellationToken.None);

        Assert.Equal(3, page.InstanceIds.Count);
        Assert.Equal("cursor-002", page.NextCursor);
    }

    [Fact]
    public async Task ListChildDepartments_60011ScopeDenied_DegradesToEmpty()
    {
        StubAccessToken();
        _server.Given(Request.Create().WithPath("/cgi-bin/department/list").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                errcode = 60011,
                errmsg = "no privilege to access/modify contact/party/agent",
            }));

        var provider = new WeComDirectoryProvider(_apiClient, NullLogger<WeComDirectoryProvider>.Instance);
        var ctx = NewContext();
        var depts = await provider.ListChildDepartmentsAsync(ctx, "1", recursive: false, CancellationToken.None);

        Assert.Empty(depts);
    }

    [Fact]
    public async Task ListDepartmentMemberIds_60020ScopeDenied_DegradesToEmpty()
    {
        StubAccessToken();
        _server.Given(Request.Create().WithPath("/cgi-bin/user/simplelist").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                errcode = 60020,
                errmsg = "callback ip not in white list",
            }));

        var provider = new WeComDirectoryProvider(_apiClient, NullLogger<WeComDirectoryProvider>.Instance);
        var ctx = NewContext();
        var ids = await provider.ListDepartmentMemberIdsAsync(ctx, "100", recursive: false, CancellationToken.None);

        // 60020 has been added to WeComErrorMapper as VisibilityScopeDenied → degraded silently.
        Assert.Empty(ids);
    }

    private void StubAccessToken()
    {
        _server.Given(Request.Create().WithPath("/cgi-bin/gettoken").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                errcode = 0,
                errmsg = "ok",
                access_token = "real-token-1",
                expires_in = 7200,
            }));
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
        _httpClient.Dispose();
        _tokenCache.Dispose();
    }
}
