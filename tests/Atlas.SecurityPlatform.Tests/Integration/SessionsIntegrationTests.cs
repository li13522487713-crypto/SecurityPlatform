using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class SessionsIntegrationTests
{
    private readonly HttpClient _client;

    public SessionsIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ForceLogoutCurrentSession_ShouldInvalidateTokenImmediately()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        using var queryResponse = await _client.GetAsync("/api/v1/sessions?pageIndex=1&pageSize=20");
        var queryPayload = await ApiResponseAssert.ReadSuccessAsync(queryResponse);

        Assert.True(queryPayload.Data.TryGetProperty("items", out var items));
        Assert.True(items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0);
        var sessionId = ApiResponseAssert.RequireString(items.EnumerateArray().First(), "sessionId");

        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        using var forceLogoutRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/sessions/{sessionId}");
        forceLogoutRequest.Headers.Authorization = new("Bearer", accessToken);
        forceLogoutRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(forceLogoutRequest, csrfToken);

        using var forceLogoutResponse = await _client.SendAsync(forceLogoutRequest);
        var forceLogoutPayload = await ApiResponseAssert.ReadSuccessAsync(forceLogoutResponse);
        Assert.Equal(ErrorCodes.Success, forceLogoutPayload.Code);

        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        using var meResponse = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task ForceLogoutWithoutIdempotencyKey_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        using var queryResponse = await _client.GetAsync("/api/v1/sessions?pageIndex=1&pageSize=20");
        var queryPayload = await ApiResponseAssert.ReadSuccessAsync(queryResponse);
        var firstSessionId = ApiResponseAssert.RequireString(
            queryPayload.Data.GetProperty("items").EnumerateArray().First(),
            "sessionId");

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/sessions/{firstSessionId}");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.Equal(ErrorCodes.IdempotencyRequired, payload.Code);
    }
}
