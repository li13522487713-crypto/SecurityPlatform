using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class MultiTenancyTests
{
    private readonly HttpClient _client;

    public MultiTenancyTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TenantHeaderMismatch_ShouldReturn403()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", "00000000-0000-0000-0000-000000000099");

        using var response = await _client.GetAsync("/api/v1/auth/me");
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
    }

    [Fact]
    public async Task AccessProtectedEndpoint_WithoutTenantHeader_ShouldReturn400()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        using var response = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AccessProtectedEndpoint_WithCorrectTenantHeader_ShouldReturnSuccess()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        IntegrationAuthHelper.SetAuthorizationHeaders(_client, token);
        using var response = await _client.GetAsync("/api/v1/auth/me");
        response.EnsureSuccessStatusCode();
    }
}
