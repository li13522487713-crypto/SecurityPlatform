using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;
using Xunit;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class ApprovalWorkflowIntegrationTests
{
    private readonly HttpClient _client;

    public ApprovalWorkflowIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task WorkflowStepTypes_ShouldReturnNonEmptyList()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);

        using var response = await _client.GetAsync("/api/v1/workflows/step-types");
        var payload = await ApiResponseAssert.ReadSuccessAsync(response);
        Assert.Equal(JsonValueKind.Array, payload.Data.ValueKind);
        Assert.True(payload.Data.GetArrayLength() > 0);
    }

    [Fact]
    public async Task ApprovalFlowValidation_WithInvalidDefinition_ShouldReturn400()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/approval/flows/validation");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(request, csrfToken);
        request.Content = JsonContent.Create(new
        {
            name = "无效流程定义",
            definitionJson = "{}",
            description = "用于验证语义校验返回"
        });

        using var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("DefinitionJson", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApprovalFlowQuery_ShouldReturnPagedResult()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        IntegrationAuthHelper.SetAuthorizationHeaders(_client, accessToken);
        _client.DefaultRequestHeaders.Remove("X-Project-Id");
        _client.DefaultRequestHeaders.Add("X-Project-Id", "1");

        using var response = await _client.GetAsync("/api/v1/approval/flows?pageIndex=1&pageSize=10");
        var payload = await ApiResponseAssert.ReadSuccessAsync(response);
        Assert.True(payload.Data.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
    }
}
