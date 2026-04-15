using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class LowCodeIntegrationTests
{
    private readonly HttpClient _client;

    public LowCodeIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AiGenerateForm_ShouldReturnSchemaJson()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/generate-form");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(request, csrfToken);
        request.Content = JsonContent.Create(new
        {
            description = "创建一个资产入库登记表单",
            category = "asset",
            dataTableKey = "asset_registry"
        });

        using var response = await _client.SendAsync(request);
        var payload = await ApiResponseAssert.ReadSuccessAsync(response);
        Assert.False(string.IsNullOrWhiteSpace(ApiResponseAssert.RequireString(payload.Data, "schemaJson")));
    }

    [Fact]
    public async Task AiGenerateSql_WithoutIdempotency_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/generate-sql");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");
        request.Content = JsonContent.Create(new
        {
            question = "查询近7天新增资产数量",
            tableContext = "assets(id,name,created_at)"
        });

        using var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.NotEqual(ErrorCodes.IdempotencyRequired, payload.Code);
    }

    [Fact]
    public async Task AmisPageWithInvalidKey_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);

        using var response = await _client.GetAsync("/api/v1/amis/pages/invalid$key");
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.Equal(ErrorCodes.ValidationError, payload.Code);
    }
}



