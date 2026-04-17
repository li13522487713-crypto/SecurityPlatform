using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;

namespace Atlas.SecurityPlatform.Tests.Integration;

/// <summary>
/// TS-15: WorkflowV2 API 契约测试。
/// 每个端点验证正常路径(200/201)和错误路径(400/401/404)的状态码与响应体格式。
/// </summary>
[Collection("Integration")]
public sealed class WorkflowV2ApiContractTests
{
    private readonly HttpClient _client;

    public WorkflowV2ApiContractTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ─── Auth Guard Tests ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetWorkflows_WithoutAuth_ShouldReturn401()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v2/workflows");
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CreateWorkflow_WithoutAuth_ShouldReturn401()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/workflows");
        req.Content = JsonContent.Create(new { name = "test", description = "", mode = 0 });
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task RunWorkflow_WithoutAuth_ShouldReturn401()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/workflows/999999/run");
        req.Content = JsonContent.Create(new { source = "draft", inputsJson = "{}" });
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ─── GET /api/v2/workflows ────────────────────────────────────────────────

    [Fact]
    public async Task GetWorkflows_WithValidToken_ShouldReturn200WithList()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v2/workflows");
        req.Headers.Authorization = new("Bearer", token);
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        req.Headers.Add("X-Project-Id", "1");

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(body);
        Assert.True(body.Success);
    }

    // ─── POST /api/v2/workflows ───────────────────────────────────────────────

    [Fact]
    public async Task CreateWorkflow_WithValidData_ShouldReturn200WithId()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/workflows");
        req.Headers.Authorization = new("Bearer", token);
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        req.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(req, csrfToken);
        req.Content = JsonContent.Create(new
        {
            name = $"contract_test_{Guid.NewGuid():N}"[..30],
            description = "API contract test",
            mode = 0
        });

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await ApiResponseAssert.ReadSuccessAsync(resp);
        Assert.True(body.Data.TryGetProperty("id", out _) || body.Data.TryGetProperty("Id", out _));
    }

    [Fact]
    public async Task CreateWorkflow_WithEmptyName_ShouldReturn400()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/workflows");
        req.Headers.Authorization = new("Bearer", token);
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        req.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(req, csrfToken);
        req.Content = JsonContent.Create(new { name = "", description = "", mode = 0 });

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ─── GET /api/v2/workflows/{id} ───────────────────────────────────────────

    [Fact]
    public async Task GetWorkflow_NonExistentId_ShouldReturn404()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v2/workflows/999999999");
        req.Headers.Authorization = new("Bearer", token);
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        req.Headers.Add("X-Project-Id", "1");

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetWorkflow_ExistingId_ShouldReturn200WithWorkflowData()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, token);
        var workflowId = await CreateWorkflowAsync(token, csrfToken);

        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/v2/workflows/{workflowId}");
        req.Headers.Authorization = new("Bearer", token);
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        req.Headers.Add("X-Project-Id", "1");

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await ApiResponseAssert.ReadSuccessAsync(resp);
        Assert.True(body.Data.TryGetProperty("id", out _) || body.Data.TryGetProperty("Id", out _));
    }

    // ─── POST /api/v2/workflows/{id}/run ─────────────────────────────────────

    [Fact]
    public async Task RunWorkflow_NonExistentId_ShouldReturn404()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/workflows/999999999/run");
        req.Headers.Authorization = new("Bearer", token);
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        req.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(req, csrfToken);
        req.Content = JsonContent.Create(new { source = "draft", inputsJson = "{}" });

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task RunWorkflow_WithInvalidSource_ShouldReturn400()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, token);
        var workflowId = await CreateWorkflowAsync(token, csrfToken);

        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/workflows/{workflowId}/run");
        req.Headers.Authorization = new("Bearer", token);
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        req.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(req, csrfToken);
        req.Content = JsonContent.Create(new { source = "published", inputsJson = "{}" });

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(body);
        Assert.False(body.Success);
    }

    // ─── POST /api/v2/workflows/{id}/validate ────────────────────────────────

    [Fact]
    public async Task ValidateCanvas_NonExistentWorkflow_ShouldReturn404()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, token);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/workflows/999999999/validate");
        req.Headers.Authorization = new("Bearer", token);
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        req.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(req, csrfToken);
        req.Content = JsonContent.Create(new { });

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ─── GET /api/v2/workflows/executions/{execId}/trace ─────────────────────

    [Fact]
    public async Task GetTrace_NonExistentExecution_ShouldReturn404()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v2/workflows/executions/999999999/trace");
        req.Headers.Authorization = new("Bearer", token);
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        req.Headers.Add("X-Project-Id", "1");

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ─── DELETE /api/v2/workflows/{id} ───────────────────────────────────────

    [Fact]
    public async Task DeleteWorkflow_NonExistentId_ShouldReturn404()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, token);

        using var req = new HttpRequestMessage(HttpMethod.Delete, "/api/v2/workflows/999999999");
        req.Headers.Authorization = new("Bearer", token);
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        req.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(req, csrfToken);

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteWorkflow_ExistingId_ShouldReturn200()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, token);
        var workflowId = await CreateWorkflowAsync(token, csrfToken);

        using var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/v2/workflows/{workflowId}");
        req.Headers.Authorization = new("Bearer", token);
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        req.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(req, csrfToken);

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ─── Response body format contract ───────────────────────────────────────

    [Fact]
    public async Task AllErrorResponses_ShouldHaveApiResponseFormat()
    {
        var token = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v2/workflows/999999999");
        req.Headers.Authorization = new("Bearer", token);
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        req.Headers.Add("X-Project-Id", "1");

        using var resp = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.False(string.IsNullOrWhiteSpace(body.Code));
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<long> CreateWorkflowAsync(string token, string csrfToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/workflows");
        req.Headers.Authorization = new("Bearer", token);
        req.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        req.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(req, csrfToken);
        req.Content = JsonContent.Create(new
        {
            name = $"contract_{Guid.NewGuid():N}"[..30],
            description = "contract test",
            mode = 0
        });

        using var resp = await _client.SendAsync(req);
        var body = await ApiResponseAssert.ReadSuccessAsync(resp);
        if (!body.Data.TryGetProperty("id", out var idElem) && !body.Data.TryGetProperty("Id", out idElem))
            throw new InvalidOperationException("missing id in create response");

        return long.Parse(idElem.GetString() ?? throw new InvalidOperationException("null id"));
    }
}
