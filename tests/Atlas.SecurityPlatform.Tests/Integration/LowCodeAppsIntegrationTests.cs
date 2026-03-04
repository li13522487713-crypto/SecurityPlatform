using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class LowCodeAppsIntegrationTests
{
    private readonly HttpClient _client;

    public LowCodeAppsIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateLowCodeAppWithoutIdempotency_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lowcode-apps");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        request.Content = JsonContent.Create(new
        {
            appKey = $"app_{Guid.NewGuid():N}".Substring(0, 12),
            name = "低代码应用-缺少幂等",
            description = "幂等校验",
            category = "integration"
        });

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.Equal(ErrorCodes.IdempotencyRequired, payload.Code);
    }

    [Fact]
    public async Task QueryLowCodeAppsWithoutAuthentication_ShouldReturn401()
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var response = await _client.GetAsync(
            "/api/v1/lowcode-apps?pageIndex=1&pageSize=10&keyword=&sortBy=id&sortDesc=true");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
