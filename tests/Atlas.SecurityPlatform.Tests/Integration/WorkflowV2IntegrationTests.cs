using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class WorkflowV2IntegrationTests
{
    private readonly HttpClient _client;

    public WorkflowV2IntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RunWithPublishedSource_BeforePublish_ShouldReturnValidationError()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var workflowId = await CreateWorkflowAsync(accessToken, csrfToken, $"it_v2_{Guid.NewGuid():N}");

        using var runRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/workflows/{workflowId}/run");
        runRequest.Headers.Authorization = new("Bearer", accessToken);
        runRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        runRequest.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(runRequest, csrfToken);
        runRequest.Content = JsonContent.Create(new
        {
            source = "published",
            inputsJson = "{}"
        });

        using var runResponse = await _client.SendAsync(runRequest);
        Assert.Equal(HttpStatusCode.BadRequest, runResponse.StatusCode);

        var payload = await runResponse.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(payload);
        Assert.Equal(ErrorCodes.ValidationError, payload.Code);
    }

    [Fact]
    public async Task RunWithDraftSource_AfterCreate_ShouldReturnExecutionId()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var workflowId = await CreateWorkflowAsync(accessToken, csrfToken, $"it_v2_{Guid.NewGuid():N}");

        using var runRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/workflows/{workflowId}/run");
        runRequest.Headers.Authorization = new("Bearer", accessToken);
        runRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        runRequest.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(runRequest, csrfToken);
        runRequest.Content = JsonContent.Create(new
        {
            source = "draft",
            inputsJson = "{}"
        });

        using var runResponse = await _client.SendAsync(runRequest);
        var payload = await ApiResponseAssert.ReadSuccessAsync(runResponse);
        Assert.True(payload.Data.TryGetProperty("executionId", out var executionId));
        Assert.False(string.IsNullOrWhiteSpace(executionId.GetString()));
    }

    private async Task<long> CreateWorkflowAsync(string accessToken, string csrfToken, string name)
    {
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v2/workflows");
        createRequest.Headers.Authorization = new("Bearer", accessToken);
        createRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        createRequest.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(createRequest, csrfToken);
        createRequest.Content = JsonContent.Create(new
        {
            name,
            description = "integration-test",
            mode = 0
        });

        using var createResponse = await _client.SendAsync(createRequest);
        var payload = await ApiResponseAssert.ReadSuccessAsync(createResponse);
        if (!payload.Data.TryGetProperty("id", out var idElement) && !payload.Data.TryGetProperty("Id", out idElement))
        {
            throw new InvalidOperationException("create workflow response missing id");
        }

        return long.Parse(idElement.GetString() ?? throw new InvalidOperationException("workflow id is null"));
    }
}
