using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class MfaIntegrationTests
{
    private readonly HttpClient _client;

    public MfaIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SetupMfa_ShouldReturnSecretAndProvisioningUri()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        using var statusResponse = await _client.GetAsync("/api/v1/mfa/status");
        var statusPayload = await ApiResponseAssert.ReadSuccessAsync(statusResponse);
        Assert.True(statusPayload.Data.TryGetProperty("mfaEnabled", out var enabledBefore));
        Assert.False(enabledBefore.GetBoolean());

        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        using var setupRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/mfa/setup");
        setupRequest.Headers.Authorization = new("Bearer", accessToken);
        setupRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(setupRequest, csrfToken);

        using var setupResponse = await _client.SendAsync(setupRequest);
        var setupPayload = await ApiResponseAssert.ReadSuccessAsync(setupResponse);
        Assert.False(string.IsNullOrWhiteSpace(ApiResponseAssert.RequireString(setupPayload.Data, "secretKey")));
        Assert.False(string.IsNullOrWhiteSpace(ApiResponseAssert.RequireString(setupPayload.Data, "provisioningUri")));
    }

    [Fact]
    public async Task SetupMfaWithoutIdempotencyKey_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/mfa/setup");
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
