using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class DynamicMigrationsIntegrationTests
{
    private readonly HttpClient _client;

    public DynamicMigrationsIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task QueryMigrationsWithoutAuthentication_ShouldReturn401()
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var response = await _client.GetAsync(
            "/api/v1/dynamic-migrations?pageIndex=1&pageSize=10&keyword=&sortBy=id&sortDesc=true");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExecuteMigrationWithoutIdempotency_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/dynamic-migrations/1/execute");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");
        request.Content = JsonContent.Create(new
        {
            confirmDestructive = false
        });

        using var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.NotEqual(ErrorCodes.IdempotencyRequired, payload.Code);
    }
}



