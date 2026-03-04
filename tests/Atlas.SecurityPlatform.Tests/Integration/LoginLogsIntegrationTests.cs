using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class LoginLogsIntegrationTests
{
    private readonly HttpClient _client;

    public LoginLogsIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task QueryLoginLogs_ShouldReturnPagedData()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        using var response = await _client.GetAsync(
            "/api/v1/login-logs?pageIndex=1&pageSize=20&username=admin");
        var payload = await ApiResponseAssert.ReadSuccessAsync(response);

        Assert.True(payload.Data.TryGetProperty("total", out var total));
        Assert.True(total.GetInt32() >= 0);
        Assert.True(payload.Data.TryGetProperty("items", out var items));
        Assert.Equal(System.Text.Json.JsonValueKind.Array, items.ValueKind);
    }

    [Fact]
    public async Task ExportLoginLogs_ShouldReturnFile()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        using var response = await _client.GetAsync(
            "/api/v1/login-logs/export?username=admin");
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.False(string.IsNullOrWhiteSpace(contentType));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
    }
}
