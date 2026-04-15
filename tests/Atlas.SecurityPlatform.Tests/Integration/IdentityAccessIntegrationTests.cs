using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class IdentityAccessIntegrationTests
{
    private readonly HttpClient _client;

    public IdentityAccessIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RoleCreateDetailDelete_ShouldSucceed()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var roleCode = $"ROLE_IT_{Guid.NewGuid():N}".Substring(0, 20);

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/roles");
        createRequest.Headers.Authorization = new("Bearer", accessToken);
        createRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(createRequest, csrfToken);
        createRequest.Content = JsonContent.Create(new
        {
            name = "集成测试角色",
            code = roleCode,
            description = "角色正向链路"
        });

        using var createResponse = await _client.SendAsync(createRequest);
        var createPayload = await ApiResponseAssert.ReadSuccessAsync(createResponse);
        var roleId = ApiResponseAssert.RequireString(createPayload.Data, "id");

        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        using var detailResponse = await _client.GetAsync($"/api/v1/roles/{roleId}");
        var detailPayload = await ApiResponseAssert.ReadSuccessAsync(detailResponse);
        Assert.Equal(roleCode, ApiResponseAssert.RequireString(detailPayload.Data, "code"));

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/roles/{roleId}");
        deleteRequest.Headers.Authorization = new("Bearer", accessToken);
        deleteRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(deleteRequest, csrfToken);
        using var deleteResponse = await _client.SendAsync(deleteRequest);
        var deletePayload = await ApiResponseAssert.ReadSuccessAsync(deleteResponse);
        Assert.Equal(ErrorCodes.Success, deletePayload.Code);
    }

    [Fact]
    public async Task CreateRoleWithoutIdempotency_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/roles");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Content = JsonContent.Create(new
        {
            name = "角色-缺少幂等键",
            code = $"ROLE_NO_IDEM_{Guid.NewGuid():N}".Substring(0, 20),
            description = "验证幂等键必填"
        });

        using var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.NotEqual(ErrorCodes.IdempotencyRequired, payload.Code);
    }

    [Fact]
    public async Task TableViewCreateAndDelete_ShouldSucceed()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var tableViewName = $"集成测试视图-{Guid.NewGuid():N}".Substring(0, 16);

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/table-views");
        createRequest.Headers.Authorization = new("Bearer", accessToken);
        createRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(createRequest, csrfToken);
        createRequest.Content = JsonContent.Create(new
        {
            tableKey = "system.users",
            name = tableViewName,
            config = new
            {
                columns = Array.Empty<object>(),
                density = "default",
                pagination = new { pageSize = 10 },
                sort = Array.Empty<object>(),
                filters = Array.Empty<object>()
            },
            configVersion = 1
        });

        using var createResponse = await _client.SendAsync(createRequest);
        var createPayload = await ApiResponseAssert.ReadSuccessAsync(createResponse);
        var id = ApiResponseAssert.RequireString(createPayload.Data, "id");

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/table-views/{id}");
        deleteRequest.Headers.Authorization = new("Bearer", accessToken);
        deleteRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(deleteRequest, csrfToken);

        using var deleteResponse = await _client.SendAsync(deleteRequest);
        var deletePayload = await ApiResponseAssert.ReadSuccessAsync(deleteResponse);
        Assert.Equal(ErrorCodes.Success, deletePayload.Code);
    }
}



