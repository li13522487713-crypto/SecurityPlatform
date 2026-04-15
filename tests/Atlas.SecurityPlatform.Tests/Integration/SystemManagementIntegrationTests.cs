using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class SystemManagementIntegrationTests
{
    private readonly HttpClient _client;

    public SystemManagementIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task QueryDictTypesAll_ShouldReturnSuccess()
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
    public async Task ReloadPluginsWithoutIdempotency_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/plugins/reload");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}


