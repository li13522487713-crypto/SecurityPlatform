using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class AuthSecurityIntegrationTests
{
    private readonly HttpClient _client;

    public AuthSecurityIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LoginRefreshAndLogout_ShouldSucceed()
    {
        var tokens = await IntegrationAuthHelper.LoginAsync(_client);

        IntegrationAuthHelper.SetAuthorizationHeaders(_client, tokens.AccessToken);
        using var meResponse = await _client.GetAsync("/api/v1/auth/me");
        var mePayload = await ApiResponseAssert.ReadSuccessAsync(meResponse);
        Assert.NotEqual(JsonValueKind.Undefined, mePayload.Data.ValueKind);
        Assert.Equal(IntegrationAuthHelper.DefaultUsername, ApiResponseAssert.RequireString(mePayload.Data, "username"));

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        using var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = tokens.RefreshToken
        });
        var refreshPayload = await ApiResponseAssert.ReadSuccessAsync(refreshResponse);
        var refreshedAccessToken = ApiResponseAssert.RequireString(refreshPayload.Data, "accessToken");

        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, refreshedAccessToken);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutRequest.Headers.Authorization = new("Bearer", refreshedAccessToken);
        logoutRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(logoutRequest, csrfToken);

        using var logoutResponse = await _client.SendAsync(logoutRequest);
        var logoutPayload = await ApiResponseAssert.ReadSuccessAsync(logoutResponse);
        Assert.Equal(ErrorCodes.Success, logoutPayload.Code);
    }

    [Fact]
    public async Task LogoutWithoutIdempotencyKey_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.NotEqual(ErrorCodes.IdempotencyRequired, payload.Code);
    }

    [Fact]
    public async Task AuthenticatedRequest_WithMismatchedTenantHeader_ShouldReturn403()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", "00000000-0000-0000-0000-000000000002");

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var payload = await ApiResponseAssert.ReadFailureAsync(response);
        Assert.Equal(ErrorCodes.CrossTenantForbidden, payload.Code);
        Assert.Equal("禁止跨租户操作。", payload.Message);
    }
}



