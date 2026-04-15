using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class DynamicDataIntegrationTests
{
    private readonly HttpClient _client;

    public DynamicDataIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DynamicMetaFieldTypes_ShouldReturnOptions()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        _client.DefaultRequestHeaders.Remove("X-Project-Id");
        _client.DefaultRequestHeaders.Add("X-Project-Id", "1");

        using var response = await _client.GetAsync("/api/v1/dynamic/meta/field-types?dbType=sqlite");
        var payload = await ApiResponseAssert.ReadSuccessAsync(response);
        Assert.Equal(JsonValueKind.Array, payload.Data.ValueKind);
        Assert.True(payload.Data.GetArrayLength() > 0);
    }

    [Fact]
    public async Task DeleteDynamicTableWithoutIdempotency_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/dynamic-tables/non-existent-table");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");

        using var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.NotEqual(ErrorCodes.IdempotencyRequired, payload.Code);
    }

    [Fact]
    public async Task TenantDataSourceTestConnection_ShouldReturnSuccess()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tenant-datasources/test");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(request, csrfToken);
        request.Content = JsonContent.Create(new
        {
            connectionString = "Data Source=:memory:",
            dbType = "SQLite"
        });

        using var response = await _client.SendAsync(request);
        var payload = await ApiResponseAssert.ReadSuccessAsync(response);
        Assert.True(payload.Data.TryGetProperty("success", out var success));
        Assert.True(success.GetBoolean());
    }
}



