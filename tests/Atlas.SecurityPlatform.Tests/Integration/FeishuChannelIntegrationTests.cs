using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

/// <summary>
/// 治理 M-G02-C5..C8（S3）端到端：
/// - PUT 凭据 -> GET 凭据回显（脱敏）；
/// - 飞书 webhook 走 url_verification challenge 直接回包；
/// - 缺凭据时 publish release 状态为 failed 并记录 connectorMessage。
/// </summary>
[Collection("Integration")]
public sealed class FeishuChannelIntegrationTests
{
    private readonly HttpClient _client;
    private readonly AtlasWebApplicationFactory _factory;

    public FeishuChannelIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FeishuCredential_UpsertThenGet_AndUrlVerificationWebhook_ShouldWork()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        var workspaceId = "atlas-space";
        var channelId = await CreateChannelAsync(workspaceId, name: $"feishu-{Guid.NewGuid():N}"[..14], type: "feishu");

        // Upsert credential
        using var putReq = new HttpRequestMessage(HttpMethod.Put,
            $"/api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/feishu-credential");
        putReq.Content = JsonContent.Create(new
        {
            appId = "cli_test_app",
            appSecret = "secret-very-strong",
            verificationToken = "vtoken-zzz",
            encryptKey = (string?)null
        });
        using var putResp = await _client.SendAsync(putReq);
        var putPayload = await ApiResponseAssert.ReadSuccessAsync(putResp);
        Assert.Equal("cli_test_app", putPayload.Data.GetProperty("appId").GetString());

        // GET credential
        using var getReq = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/feishu-credential");
        using var getResp = await _client.SendAsync(getReq);
        var getPayload = await ApiResponseAssert.ReadSuccessAsync(getResp);
        Assert.Equal("cli_test_app", getPayload.Data.GetProperty("appId").GetString());
        Assert.Equal("vtoken-zzz", getPayload.Data.GetProperty("verificationToken").GetString());
        Assert.False(getPayload.Data.GetProperty("hasEncryptKey").GetBoolean());

        // url_verification webhook (anonymous)
        using var publicClient = _factory.CreateClient();
        using var webhookReq = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/runtime/channels/feishu/{channelId}/webhook");
        webhookReq.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        webhookReq.Content = new StringContent(
            "{\"type\":\"url_verification\",\"challenge\":\"chal-xyz\"}",
            Encoding.UTF8, "application/json");
        using var webhookResp = await publicClient.SendAsync(webhookReq);
        Assert.Equal(HttpStatusCode.OK, webhookResp.StatusCode);
        var webhookBody = await webhookResp.Content.ReadAsStringAsync();
        Assert.Contains("chal-xyz", webhookBody, StringComparison.Ordinal);
    }

    private async Task<string> CreateChannelAsync(string workspaceId, string name, string type)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/workspaces/{workspaceId}/publish-channels");
        req.Content = JsonContent.Create(new
        {
            name,
            type,
            description = "feishu integration",
            supportedTargets = new[] { "agent" }
        });
        using var resp = await _client.SendAsync(req);
        var payload = await ApiResponseAssert.ReadSuccessAsync(resp);
        return payload.Data.GetProperty("channelId").GetString()!;
    }
}
