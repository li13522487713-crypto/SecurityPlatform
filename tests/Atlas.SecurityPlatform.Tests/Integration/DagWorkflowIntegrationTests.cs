using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Core.Models;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class DagWorkflowIntegrationTests
{
    private readonly HttpClient _client;

    public DagWorkflowIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RunWithPublishedSource_BeforePublish_ShouldReturnValidationError()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var workflowId = await CreateWorkflowAsync(accessToken, csrfToken, $"it_v2_{Guid.NewGuid():N}"[..30]);

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
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, runResponse.StatusCode);
    }

    [Fact]
    public async Task RunWithDraftSource_AfterCreate_ShouldReturnExecutionId()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var workflowId = await CreateWorkflowAsync(accessToken, csrfToken, $"it_v2_{Guid.NewGuid():N}"[..30]);

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

    [Fact]
    public async Task GetWorkflow_WithPublishedSource_ShouldReturnPublishedCanvasInsteadOfLatestDraft()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var workflowId = await CreateWorkflowAsync(accessToken, csrfToken, $"it_v2_published_{Guid.NewGuid():N}"[..30]);

        var publishedCanvas = BuildTextWorkflowCanvas("已发布版本");
        await SaveDraftAsync(accessToken, csrfToken, workflowId, publishedCanvas);
        var savedDraftDetail = await GetWorkflowDetailAsync(accessToken, workflowId, "?source=draft");
        Assert.Equal("已发布版本", GetTextProcessorTemplate(savedDraftDetail));
        await PublishAsync(accessToken, csrfToken, workflowId, "发布初版");

        var draftCanvas = BuildTextWorkflowCanvas("草稿新内容");
        await SaveDraftAsync(accessToken, csrfToken, workflowId, draftCanvas);

        var publishedDetail = await GetWorkflowDetailAsync(accessToken, workflowId, "?source=published");
        Assert.Equal("已发布版本", GetTextProcessorTemplate(publishedDetail));

        var draftDetail = await GetWorkflowDetailAsync(accessToken, workflowId, "?source=draft");
        Assert.Equal("草稿新内容", GetTextProcessorTemplate(draftDetail));
    }

    [Fact]
    public async Task ValidateCanvas_WithUnsavedCanvasJson_ShouldReturnValidationResult()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var workflowId = await CreateWorkflowAsync(accessToken, csrfToken, $"it_v2_validate_{Guid.NewGuid():N}"[..30]);

        using var validateRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/workflows/{workflowId}/validate");
        validateRequest.Headers.Authorization = new("Bearer", accessToken);
        validateRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        validateRequest.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(validateRequest, csrfToken);
        validateRequest.Content = JsonContent.Create(new
        {
            canvasJson = BuildTextWorkflowCanvas("未保存校验")
        });

        using var validateResponse = await _client.SendAsync(validateRequest);
        var payload = await ApiResponseAssert.ReadSuccessAsync(validateResponse);

        Assert.True(payload.Data.TryGetProperty("isValid", out var isValid));
        Assert.Equal(JsonValueKind.True, isValid.ValueKind);
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

    private async Task SaveDraftAsync(string accessToken, string csrfToken, long workflowId, string canvasJson)
    {
        using var saveRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v2/workflows/{workflowId}/draft");
        saveRequest.Headers.Authorization = new("Bearer", accessToken);
        saveRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        saveRequest.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(saveRequest, csrfToken);
        saveRequest.Content = JsonContent.Create(new
        {
            canvasJson,
            commitId = $"draft-{Guid.NewGuid():N}"
        });

        using var saveResponse = await _client.SendAsync(saveRequest);
        await ApiResponseAssert.ReadSuccessAsync(saveResponse);
    }

    private async Task PublishAsync(string accessToken, string csrfToken, long workflowId, string changeLog)
    {
        using var publishRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/workflows/{workflowId}/publish");
        publishRequest.Headers.Authorization = new("Bearer", accessToken);
        publishRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        publishRequest.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(publishRequest, csrfToken);
        publishRequest.Content = JsonContent.Create(new
        {
            changeLog
        });

        using var publishResponse = await _client.SendAsync(publishRequest);
        await ApiResponseAssert.ReadSuccessAsync(publishResponse);
    }

    private async Task<string> GetWorkflowDetailAsync(string accessToken, long workflowId, string query = "")
    {
        using var detailRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v2/workflows/{workflowId}{query}");
        detailRequest.Headers.Authorization = new("Bearer", accessToken);
        detailRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        detailRequest.Headers.Add("X-Project-Id", "1");

        using var detailResponse = await _client.SendAsync(detailRequest);
        var payload = await ApiResponseAssert.ReadSuccessAsync(detailResponse);
        return payload.Data.GetProperty("canvasJson").GetString() ?? string.Empty;
    }

    private static string BuildTextWorkflowCanvas(string template)
    {
        return $$"""
        {
          "nodes": [
            {
              "key": "entry_1",
              "type": 1,
              "label": "开始",
              "config": {},
              "layout": { "x": 120, "y": 120, "width": 160, "height": 60 }
            },
            {
              "key": "text_1",
              "type": 15,
              "label": "文本处理",
              "config": {
                "template": "{{template}}",
                "outputKey": "text_output"
              },
              "layout": { "x": 380, "y": 120, "width": 160, "height": 60 }
            },
            {
              "key": "exit_1",
              "type": 2,
              "label": "结束",
              "config": {},
              "layout": { "x": 640, "y": 120, "width": 160, "height": 60 }
            }
          ],
          "connections": [
            {
              "sourceNodeKey": "entry_1",
              "sourcePort": "output",
              "targetNodeKey": "text_1",
              "targetPort": "input",
              "condition": null
            },
            {
              "sourceNodeKey": "text_1",
              "sourcePort": "output",
              "targetNodeKey": "exit_1",
              "targetPort": "input",
              "condition": null
            }
          ]
        }
        """;
    }

    private static string GetTextProcessorTemplate(string canvasJson)
    {
        using var document = JsonDocument.Parse(canvasJson);
        var textNode = document.RootElement
            .GetProperty("nodes")
            .EnumerateArray()
            .First(node => node.GetProperty("key").GetString() == "text_1");

        return textNode.GetProperty("config").GetProperty("template").GetString() ?? string.Empty;
    }
}
