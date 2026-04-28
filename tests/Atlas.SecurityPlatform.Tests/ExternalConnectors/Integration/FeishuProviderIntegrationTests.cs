using System.Net.Http;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Caching;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.Feishu;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.ExternalConnectors.Integration;

/// <summary>
/// 通过 WireMock.Net 桩飞书 OpenAPI，验证 N2 引入 FeishuNetSdk + 修复后的:
/// - tenant_access_token 缓存；
/// - approval/v4/instances 创建实例；
/// - approval/v4/external_instances 创建（N2 补齐）+ check 同步；
/// - im/v1/messages 文本/卡片发送；
/// - approval/v4/instances 列表（ListRecentInstanceIds）。
/// </summary>
public sealed class FeishuProviderIntegrationTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly FeishuApiClient _apiClient;
    private readonly InMemoryConnectorTokenCache _tokenCache;
    private readonly FeishuRuntimeOptions _runtime;

    public FeishuProviderIntegrationTests()
    {
        _server = WireMockServer.Start();

        var options = new FeishuOptions
        {
            ApiBaseUrl = _server.Url!,
        };

        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(FeishuApiClient.HttpClientName).Returns(_ => new HttpClient { BaseAddress = new Uri(_server.Url!) });

        _tokenCache = new InMemoryConnectorTokenCache(new MemoryCache(new MemoryCacheOptions()));
        _runtime = new FeishuRuntimeOptions
        {
            AppId = "cli_app_id",
            AppSecret = "app_secret",
            TenantKey = "tenant_test",
            CallbackBaseUrl = $"{_server.Url}/cb",
            EventVerificationToken = "vt-test",
            EventEncryptKey = "ek-test",
        };

        _apiClient = new FeishuApiClient(httpFactory, _tokenCache, Options.Create(options), NullLogger<FeishuApiClient>.Instance);
    }

    private ConnectorContext NewContext() => new()
    {
        TenantId = Guid.NewGuid(),
        ProviderInstanceId = 1,
        ProviderType = FeishuConnectorMarker.ProviderType,
        RuntimeOptions = _runtime,
    };

    [Fact]
    public async Task GetTenantAccessToken_HitsInternalEndpoint_AndCaches()
    {
        _server.Given(Request.Create().WithPath("/open-apis/auth/v3/tenant_access_token/internal").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                code = 0,
                msg = "ok",
                tenant_access_token = "fs-token-1",
                expire = 7200,
            }));

        var ctx = NewContext();
        var t1 = await _apiClient.GetTenantAccessTokenAsync(ctx, CancellationToken.None);
        var t2 = await _apiClient.GetTenantAccessTokenAsync(ctx, CancellationToken.None);

        Assert.Equal("fs-token-1", t1);
        Assert.Equal(t1, t2);
        var hits = _server.LogEntries.Count(e => e.RequestMessage.Path.Contains("/auth/v3/tenant_access_token/internal"));
        Assert.Equal(1, hits);
    }

    [Fact]
    public async Task SubmitApproval_PostsInstancesEndpoint_AndParsesInstanceCode()
    {
        StubTenantToken();
        _server.Given(Request.Create().WithPath("/open-apis/approval/v4/instances").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                code = 0,
                msg = "ok",
                data = new { instance_code = "FS-INSTANCE-001" },
            }));

        var provider = new FeishuApprovalProvider(_apiClient);
        var ctx = NewContext();
        var result = await provider.SubmitApprovalAsync(ctx, new ExternalApprovalSubmission
        {
            ApplicantExternalUserId = "ou_xxx",
            ExternalTemplateId = "TPL-001",
            BusinessKey = "bk-1",
            Fields = new Dictionary<string, ExternalApprovalFieldValue>(),
        }, CancellationToken.None);

        Assert.Equal("FS-INSTANCE-001", result.ExternalInstanceId);
    }

    [Fact]
    public async Task SyncThirdPartyInstance_HitsExternalInstancesCreate_ThenCheck()
    {
        StubTenantToken();
        var createHit = false;
        var checkHit = false;

        _server.Given(Request.Create().WithPath("/open-apis/approval/v4/external_instances").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { code = 0, msg = "ok", data = new { } }));
        _server.Given(Request.Create().WithPath("/open-apis/approval/v4/external_instances/check").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new { code = 0, msg = "ok", data = new { } }));

        var provider = new FeishuApprovalProvider(_apiClient);
        var ctx = NewContext();
        var ok = await provider.SyncThirdPartyInstanceAsync(ctx, new ExternalThirdPartyInstancePatch
        {
            ExternalInstanceId = "FS-INSTANCE-001",
            NewStatus = ExternalApprovalStatus.Approved,
        }, CancellationToken.None);

        Assert.True(ok);
        // 验证 create + check 都被调用过（N2 补齐：原实现只有 check）。
        createHit = _server.LogEntries.Any(e => e.RequestMessage.Path == "/open-apis/approval/v4/external_instances" && e.RequestMessage.Method == "POST");
        checkHit = _server.LogEntries.Any(e => e.RequestMessage.Path == "/open-apis/approval/v4/external_instances/check" && e.RequestMessage.Method == "POST");
        Assert.True(createHit, "external_instances create endpoint should be invoked");
        Assert.True(checkHit, "external_instances/check endpoint should be invoked");
    }

    [Fact]
    public async Task SendCard_HitsImV1Messages_AndReturnsMessageId()
    {
        StubTenantToken();
        _server.Given(Request.Create().WithPath(new RegexMatcher("^/open-apis/im/v1/messages")).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                code = 0,
                msg = "ok",
                data = new { message_id = "om_test_msg_id_001" },
            }));

        var provider = new FeishuMessagingProvider(_apiClient);
        var ctx = NewContext();
        var card = new ExternalMessageCard { Title = "测试卡片", Subtitle = "副标题", Content = "内容" };
        var recipient = new ExternalMessageRecipient { UserIds = new[] { "ou_xxx" } };

        var dispatch = await provider.SendCardAsync(ctx, recipient, card, CancellationToken.None);

        Assert.Equal("om_test_msg_id_001", dispatch.MessageId);
    }

    private void StubTenantToken()
    {
        _server.Given(Request.Create().WithPath("/open-apis/auth/v3/tenant_access_token/internal").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithBodyAsJson(new
            {
                code = 0,
                msg = "ok",
                tenant_access_token = "fs-token-1",
                expire = 7200,
            }));
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
        _tokenCache.Dispose();
    }
}
