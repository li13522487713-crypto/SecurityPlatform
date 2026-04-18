using System.Net.Http;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Caching;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.DingTalk;
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
/// 通过 WireMock.Net 桩钉钉 OpenAPI（v1.0 + v1 老版），验证 N3 全套实现：
/// - v1.0 /oauth2/accessToken 缓存；
/// - v1.0 /workflow/processInstances 创建与查询；
/// - v1 /topapi/v2/department/listsub 子部门 + 60011 降级；
/// - v1 /topapi/message/corpconversation/asyncsend_v2 工作通知。
/// </summary>
public sealed class DingTalkProviderIntegrationTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly DingTalkApiClient _apiClient;
    private readonly InMemoryConnectorTokenCache _tokenCache;

    public DingTalkProviderIntegrationTests()
    {
        _server = WireMockServer.Start();

        var options = new DingTalkOptions
        {
            ApiBaseUrl = _server.Url!,
            LegacyApiBaseUrl = _server.Url!,
            OAuthBaseUrl = _server.Url!,
        };

        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(DingTalkApiClient.HttpClientName).Returns(_ => new HttpClient { BaseAddress = new Uri(_server.Url!) });

        _tokenCache = new InMemoryConnectorTokenCache(new MemoryCache(new MemoryCacheOptions()));

        var runtimeResolver = Substitute.For<IConnectorRuntimeOptionsResolver<DingTalkRuntimeOptions>>();
        runtimeResolver.ResolveAsync(Arg.Any<ConnectorContext>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new DingTalkRuntimeOptions
            {
                AppKey = "ding_app_key",
                AppSecret = "ding_app_secret",
                AgentId = "1234567890",
                CorpId = "ding_corp_id",
                CallbackBaseUrl = $"{_server.Url}/cb",
                TrustedDomains = new[] { "platform.example.com" },
            }));

        _apiClient = new DingTalkApiClient(httpFactory, _tokenCache, runtimeResolver, Options.Create(options), NullLogger<DingTalkApiClient>.Instance);
    }

    [Fact]
    public async Task GetAccessToken_HitsV1OAuth2Endpoint_AndCaches()
    {
        _server.Given(Request.Create().WithPath("/v1.0/oauth2/accessToken").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                accessToken = "ding-token-1",
                expireIn = 7200,
            }));

        var ctx = new ConnectorContext { TenantId = Guid.NewGuid(), ProviderInstanceId = 1, ProviderType = "dingtalk" };
        var t1 = await _apiClient.GetAccessTokenAsync(ctx, CancellationToken.None);
        var t2 = await _apiClient.GetAccessTokenAsync(ctx, CancellationToken.None);

        Assert.Equal("ding-token-1", t1);
        Assert.Equal(t1, t2);
        var hits = _server.LogEntries.Count(e => e.RequestMessage.Path.Contains("/v1.0/oauth2/accessToken"));
        Assert.Equal(1, hits);
    }

    [Fact]
    public async Task SubmitApproval_HitsV1WorkflowProcessInstances()
    {
        StubAccessToken();
        _server.Given(Request.Create().WithPath("/v1.0/workflow/processInstances").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { instanceId = "DING-INSTANCE-001" }));

        var provider = new DingTalkApprovalProvider(_apiClient);
        var ctx = new ConnectorContext { TenantId = Guid.NewGuid(), ProviderInstanceId = 1, ProviderType = "dingtalk" };
        var ref_ = await provider.SubmitApprovalAsync(ctx, new ExternalApprovalSubmission
        {
            ApplicantExternalUserId = "ding_zhangsan",
            ExternalTemplateId = "PROC_LEAVE_001",
            BusinessKey = "bk-1",
            Fields = new Dictionary<string, ExternalApprovalFieldValue>(),
        }, CancellationToken.None);

        Assert.Equal("DING-INSTANCE-001", ref_.ExternalInstanceId);
    }

    [Fact]
    public async Task ListChildDepartments_60011ScopeDenied_DegradesToEmpty()
    {
        StubAccessToken();
        _server.Given(Request.Create().WithPath("/topapi/v2/department/listsub").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                errcode = 60011,
                errmsg = "no privilege",
            }));

        var provider = new DingTalkDirectoryProvider(_apiClient, NullLogger<DingTalkDirectoryProvider>.Instance);
        var ctx = new ConnectorContext { TenantId = Guid.NewGuid(), ProviderInstanceId = 1, ProviderType = "dingtalk" };
        var depts = await provider.ListChildDepartmentsAsync(ctx, "1", recursive: false, CancellationToken.None);

        Assert.Empty(depts);
    }

    [Fact]
    public async Task SendText_HitsAsyncSendV2_AndReturnsTaskId()
    {
        StubAccessToken();
        _server.Given(Request.Create().WithPath("/topapi/message/corpconversation/asyncsend_v2").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                errcode = 0,
                errmsg = "ok",
                task_id = 99988877766L,
            }));

        var provider = new DingTalkMessagingProvider(_apiClient);
        var ctx = new ConnectorContext { TenantId = Guid.NewGuid(), ProviderInstanceId = 1, ProviderType = "dingtalk" };
        var dispatch = await provider.SendTextAsync(ctx, new ExternalMessageRecipient { UserIds = new[] { "ding_zhangsan" } }, "测试", CancellationToken.None);

        Assert.Equal("99988877766", dispatch.MessageId);
    }

    private void StubAccessToken()
    {
        _server.Given(Request.Create().WithPath("/v1.0/oauth2/accessToken").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                accessToken = "ding-token-1",
                expireIn = 7200,
            }));
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
        _tokenCache.Dispose();
    }
}
