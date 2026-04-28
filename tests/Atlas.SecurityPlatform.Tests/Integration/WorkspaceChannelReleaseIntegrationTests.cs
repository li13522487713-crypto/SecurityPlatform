using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

/// <summary>
/// 治理 M-G02-C2（S1）端到端：
/// - 创建一个 wechat 渠道（部署侧未注册 connector）；
/// - 发布一次 → 期望 200 + status=failed + 审计写入；
/// - 列表 → 看到该 release；
/// - 回滚到不存在的 releaseId → 4xx；
/// - GET releases/{id} → 与 list 一致。
/// </summary>
[Collection("Integration")]
public sealed class WorkspaceChannelReleaseIntegrationTests
{
    private readonly HttpClient _client;

    public WorkspaceChannelReleaseIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PublishRelease_WhenNoConnectorRegistered_ShouldReturnFailedRelease()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        var workspaceId = "atlas-space";
        var channelId = await CreateChannelAsync(workspaceId, name: $"chan-{Guid.NewGuid():N}"[..16], type: "wechat");

        using var publishReq = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/releases");
        publishReq.Content = JsonContent.Create(new
        {
            agentId = "1001",
            releaseNote = "S1 integration"
        });

        using var publishResp = await _client.SendAsync(publishReq);
        var publishPayload = await ApiResponseAssert.ReadSuccessAsync(publishResp);
        var release = publishPayload.Data;
        Assert.Equal("failed", release.GetProperty("status").GetString());
        Assert.Equal(1, release.GetProperty("releaseNo").GetInt32());
        var connectorMessage = release.GetProperty("connectorMessage").GetString();
        Assert.False(string.IsNullOrWhiteSpace(connectorMessage));
        Assert.Contains("not registered", connectorMessage, StringComparison.OrdinalIgnoreCase);
        var releaseId = release.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(releaseId));

        // List should contain it
        using var listResp = await _client.GetAsync(
            $"/api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/releases?pageIndex=1&pageSize=10");
        var listPayload = await ApiResponseAssert.ReadSuccessAsync(listResp);
        var totalProperty = listPayload.Data.GetProperty("total");
        var total = totalProperty.ValueKind == JsonValueKind.String
            ? long.Parse(totalProperty.GetString()!)
            : totalProperty.GetInt64();
        Assert.True(total >= 1);
        var items = listPayload.Data.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(items, item => item.GetProperty("id").GetString() == releaseId);

        // Get single by id
        using var getResp = await _client.GetAsync(
            $"/api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/releases/{releaseId}");
        var getPayload = await ApiResponseAssert.ReadSuccessAsync(getResp);
        Assert.Equal(releaseId, getPayload.Data.GetProperty("id").GetString());

        // Rollback to a missing release id should 4xx
        using var rollbackReq = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/workspaces/{workspaceId}/publish-channels/{channelId}/releases/rollback");
        rollbackReq.Content = JsonContent.Create(new
        {
            targetReleaseId = "9999999999",
            releaseNote = "missing"
        });
        using var rollbackResp = await _client.SendAsync(rollbackReq);
        Assert.True(
            rollbackResp.StatusCode == HttpStatusCode.NotFound ||
            rollbackResp.StatusCode == HttpStatusCode.OK, // 业务异常通过 ApiResponse 包装时仍为 200
            $"unexpected status: {(int)rollbackResp.StatusCode}");

        if (rollbackResp.StatusCode == HttpStatusCode.OK)
        {
            var rollbackPayload = await rollbackResp.Content.ReadFromJsonAsync<
                Atlas.Core.Models.ApiResponse<JsonElement>>();
            Assert.NotNull(rollbackPayload);
            Assert.False(rollbackPayload.Success);
        }
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
