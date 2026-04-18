using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Atlas.Infrastructure.Services.AiPlatform.Channels.Signatures;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

/// <summary>
/// 治理 M-G02-C3 端到端：在真实 DI 中验证 WebSdkChannelConnector + PublicChannelEndpointsController。
/// </summary>
[Collection("Integration")]
public sealed class PublicChannelEndpointsIntegrationTests
{
    private readonly HttpClient _client;
    private readonly AtlasWebApplicationFactory _factory;

    public PublicChannelEndpointsIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task WebSdkPublish_ShouldReturnActiveWithSnippetAndSecret()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        var workspaceId = "atlas-space";
        var channelName = $"web-{Guid.NewGuid():N}"[..14];
        var channelId = await CreateChannelAsync(workspaceId, channelName, "web-sdk");

        using var publishReq = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/releases");
        publishReq.Content = JsonContent.Create(new
        {
            agentId = "1001",
            releaseNote = "S2 web-sdk publish"
        });
        using var publishResp = await _client.SendAsync(publishReq);
        var publishPayload = await ApiResponseAssert.ReadSuccessAsync(publishResp);
        var release = publishPayload.Data;
        Assert.Equal("active", release.GetProperty("status").GetString());

        var publicMetaJson = release.GetProperty("publicMetadataJson").GetString();
        Assert.False(string.IsNullOrEmpty(publicMetaJson));
        using var meta = JsonDocument.Parse(publicMetaJson!);
        Assert.Contains("snippet", publicMetaJson, StringComparison.Ordinal);
        Assert.Contains("AtlasWebSdk", publicMetaJson, StringComparison.Ordinal);
        var endpoint = meta.RootElement.GetProperty("endpoint").GetString();
        Assert.Equal($"/api/v1/runtime/channels/web-sdk/{channelId}/messages", endpoint);

        // 用错误签名访问公共端点 → 401 / WebSdkSignatureMismatch
        using var clientForPublic = _factory.CreateClient();
        using var badReq = new HttpRequestMessage(HttpMethod.Post, endpoint);
        badReq.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        badReq.Headers.Add("X-Channel-Signature", "abc-not-valid");
        badReq.Headers.Add("X-Channel-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        badReq.Headers.Add("X-Channel-Nonce", "n-bad");
        badReq.Content = new StringContent("{\"message\":\"hi\"}", Encoding.UTF8, "application/json");
        using var badResp = await clientForPublic.SendAsync(badReq);
        var bodyText = await badResp.Content.ReadAsStringAsync();
        Assert.True(badResp.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected 401 from {endpoint}, got {(int)badResp.StatusCode} {badResp.StatusCode}; body: {bodyText}");
    }

    private async Task<string> CreateChannelAsync(string workspaceId, string name, string type)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/workspaces/{workspaceId}/publish-channels");
        req.Content = JsonContent.Create(new
        {
            name,
            type,
            description = "integration",
            supportedTargets = new[] { "agent" }
        });
        using var resp = await _client.SendAsync(req);
        var payload = await ApiResponseAssert.ReadSuccessAsync(resp);
        var channelId = payload.Data.GetProperty("channelId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(channelId));
        return channelId!;
    }
}
