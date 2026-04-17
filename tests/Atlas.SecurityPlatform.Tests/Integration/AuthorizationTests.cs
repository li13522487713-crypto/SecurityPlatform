using System.Net;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class AuthorizationTests
{
    private readonly HttpClient _client;

    public AuthorizationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AccessProtectedEndpoint_WithoutAuthentication_ShouldReturn401()
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var response = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AccessProtectedEndpoint_WithValidToken_ShouldReturn200()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        using var response = await _client.GetAsync("/api/v1/auth/me");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task AccessProtectedEndpoint_WithMismatchedTenantHeader_ShouldReturn403()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", "00000000-0000-0000-0000-000000000099");

        using var response = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/assets?pageIndex=1&pageSize=1")]
    [InlineData("/api/v1/users?pageIndex=1&pageSize=1")]
    [InlineData("/api/v2/application-catalogs?pageIndex=1&pageSize=1")]
    public async Task AccessMultiResource_WithMismatchedTenantHeader_ShouldReturn403(string endpoint)
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", "00000000-0000-0000-0000-000000000099");

        using var response = await _client.GetAsync(endpoint);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
