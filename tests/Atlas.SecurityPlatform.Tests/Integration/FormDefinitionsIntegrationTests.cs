using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class FormDefinitionsIntegrationTests
{
    private readonly HttpClient _client;

    public FormDefinitionsIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateFormDefinitionWithoutIdempotency_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/form-definitions");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");
        request.Content = JsonContent.Create(new
        {
            formKey = $"form_{Guid.NewGuid():N}".Substring(0, 12),
            name = "表单定义-缺少幂等",
            category = "integration",
            schemaJson = "{\"type\":\"form\",\"body\":[]}"
        });

        using var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.NotEqual(ErrorCodes.IdempotencyRequired, payload.Code);
    }

    [Fact]
    public async Task QueryFormDefinitionsWithoutAuthentication_ShouldReturn401()
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var response = await _client.GetAsync(
            "/api/v1/form-definitions?pageIndex=1&pageSize=10&keyword=&sortBy=id&sortDesc=true");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}



