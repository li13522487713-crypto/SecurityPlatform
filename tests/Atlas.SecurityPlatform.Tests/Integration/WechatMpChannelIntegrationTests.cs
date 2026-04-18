using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class WechatMpChannelIntegrationTests
{
    private readonly HttpClient _client;
    private readonly AtlasWebApplicationFactory _factory;

    public WechatMpChannelIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task UpsertGet_AndGetWebhookVerify_ShouldWork()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        var workspaceId = "atlas-space";
        var channelId = await CreateChannelAsync(workspaceId, name: $"wxmp-{Guid.NewGuid():N}"[..14], type: "wechat-mp");

        const string token = "wechat-test-token";
        // upsert credential
        using var putReq = new HttpRequestMessage(HttpMethod.Put,
            $"/api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/wechat-mp-credential");
        putReq.Content = JsonContent.Create(new
        {
            appId = "wx_app_id",
            appSecret = "wx_app_secret",
            token,
            encodingAesKey = (string?)null
        });
        using var putResp = await _client.SendAsync(putReq);
        var putPayload = await ApiResponseAssert.ReadSuccessAsync(putResp);
        Assert.Equal("wx_app_id", putPayload.Data.GetProperty("appId").GetString());
        Assert.Equal(token, putPayload.Data.GetProperty("token").GetString());

        // GET verification (signature with token+timestamp+nonce sorted)
        using var publicClient = _factory.CreateClient();
        var ts = "1700000000";
        var nonce = "n-int";
        var signature = ComputeSignature(token, ts, nonce);
        using var getReq = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/runtime/channels/wechat-mp/{channelId}/webhook?signature={signature}&timestamp={ts}&nonce={nonce}&echostr=hello-echo");
        getReq.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        using var getResp = await publicClient.SendAsync(getReq);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var body = await getResp.Content.ReadAsStringAsync();
        Assert.Equal("hello-echo", body.Trim());
    }

    private static string ComputeSignature(string token, string ts, string nonce)
    {
        var arr = new[] { token, ts, nonce };
        Array.Sort(arr, StringComparer.Ordinal);
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(string.Concat(arr)));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private async Task<string> CreateChannelAsync(string workspaceId, string name, string type)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/workspaces/{workspaceId}/publish-channels");
        req.Content = JsonContent.Create(new
        {
            name,
            type,
            description = "wxmp integration",
            supportedTargets = new[] { "agent" }
        });
        using var resp = await _client.SendAsync(req);
        var payload = await ApiResponseAssert.ReadSuccessAsync(resp);
        return payload.Data.GetProperty("channelId").GetString()!;
    }
}
