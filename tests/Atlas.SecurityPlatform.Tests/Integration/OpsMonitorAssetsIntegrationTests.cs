using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class OpsMonitorAssetsIntegrationTests
{
    private readonly HttpClient _client;

    public OpsMonitorAssetsIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MonitorServerInfo_ShouldReturnSnapshot()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        _client.DefaultRequestHeaders.Remove("X-Project-Id");
        _client.DefaultRequestHeaders.Add("X-Project-Id", "1");

        using var response = await _client.GetAsync("/api/v1/monitor/server-info");
        var payload = await ApiResponseAssert.ReadSuccessAsync(response);
        Assert.NotEqual(JsonValueKind.Undefined, payload.Data.ValueKind);
        Assert.NotEqual(JsonValueKind.Null, payload.Data.ValueKind);
    }

    [Fact]
    public async Task DictTypesAll_ShouldReturnList()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        _client.DefaultRequestHeaders.Remove("X-Project-Id");
        _client.DefaultRequestHeaders.Add("X-Project-Id", "1");

        using var response = await _client.GetAsync("/api/v1/dict-types/all");
        var payload = await ApiResponseAssert.ReadSuccessAsync(response);
        Assert.Equal(JsonValueKind.Array, payload.Data.ValueKind);
    }

    [Fact]
    public async Task AssetCreateWithoutIdempotency_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/assets");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");
        request.Content = JsonContent.Create(new
        {
            name = $"集成测试资产-{Guid.NewGuid():N}".Substring(0, 16)
        });

        using var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.NotEqual(ErrorCodes.IdempotencyRequired, payload.Code);
    }
}



