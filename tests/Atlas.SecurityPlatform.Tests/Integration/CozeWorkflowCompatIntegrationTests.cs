using System.Net.Http.Json;
using System.Text.Json;
using Atlas.SecurityPlatform.Tests.Integration.Infrastructure;

namespace Atlas.SecurityPlatform.Tests.Integration;

[Collection("Integration")]
public sealed class CozeWorkflowCompatIntegrationTests
{
    private readonly HttpClient _client;

    public CozeWorkflowCompatIntegrationTests(AtlasWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SaveWorkflow_ShouldPreserveCozeNativeSchema()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var workflowId = await CreateWorkflowAsync(accessToken, csrfToken, $"coze_{Guid.NewGuid():N}"[..20]);
        var schema = BuildCozeNativeSchema("incident");

        await SaveWorkflowAsync(accessToken, csrfToken, workflowId, schema);

        using var canvasRequest = new HttpRequestMessage(HttpMethod.Post, "/api/workflow_api/canvas");
        canvasRequest.Headers.Authorization = new("Bearer", accessToken);
        canvasRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        canvasRequest.Headers.Add("X-Project-Id", "1");
        canvasRequest.Content = JsonContent.Create(new
        {
            workflow_id = workflowId.ToString(),
            space_id = "atlas-workflow"
        });

        using var canvasResponse = await _client.SendAsync(canvasRequest);
        var payload = await ApiResponseAssert.ReadCozeSuccessAsync(canvasResponse);
        var schemaJson = payload.GetProperty("workflow").GetProperty("schema_json").GetString() ?? string.Empty;

        using var document = JsonDocument.Parse(schemaJson);
        Assert.True(document.RootElement.TryGetProperty("edges", out var edges));
        Assert.Equal(2, edges.GetArrayLength());
        Assert.Equal("entry_1", document.RootElement.GetProperty("nodes")[0].GetProperty("id").GetString());
        Assert.True(document.RootElement.GetProperty("nodes")[0].TryGetProperty("data", out _));
    }

    [Fact]
    public async Task TestRun_ShouldCompileAndExecuteCozeNativeSchema()
    {
        var accessToken = await IntegrationAuthHelper.LoginAndGetAccessTokenAsync(_client);
        var csrfToken = await CsrfIdempotencyHelper.GetAntiforgeryTokenAsync(_client, accessToken);
        var workflowId = await CreateWorkflowAsync(accessToken, csrfToken, $"coze_{Guid.NewGuid():N}"[..20]);
        await SaveWorkflowAsync(accessToken, csrfToken, workflowId, BuildCozeNativeSchema("incident"));

        using var runRequest = new HttpRequestMessage(HttpMethod.Post, "/api/workflow_api/test_run");
        runRequest.Headers.Authorization = new("Bearer", accessToken);
        runRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        runRequest.Headers.Add("X-Project-Id", "1");
        runRequest.Content = JsonContent.Create(new
        {
            workflow_id = workflowId.ToString(),
            space_id = "atlas-workflow",
            input = new Dictionary<string, string>
            {
                ["incident"] = "hello"
            }
        });

        using var runResponse = await _client.SendAsync(runRequest);
        var runPayload = await ApiResponseAssert.ReadCozeSuccessAsync(runResponse);
        var executionId = runPayload.GetProperty("execute_id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(executionId));

        using var processRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/workflow_api/get_process?workflow_id={workflowId}&execute_id={executionId}");
        processRequest.Headers.Authorization = new("Bearer", accessToken);
        processRequest.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        processRequest.Headers.Add("X-Project-Id", "1");

        using var processResponse = await _client.SendAsync(processRequest);
        var processPayload = await ApiResponseAssert.ReadCozeSuccessAsync(processResponse);
        Assert.Equal(2, processPayload.GetProperty("executeStatus").GetInt32());
        Assert.True(processPayload.GetProperty("nodeResults").GetArrayLength() >= 1);
    }

    private async Task<long> CreateWorkflowAsync(string accessToken, string csrfToken, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/workflow_api/create");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(request, csrfToken);
        request.Content = JsonContent.Create(new
        {
            name,
            desc = "coze compat integration",
            space_id = "atlas-workflow",
            flow_mode = 0
        });

        using var response = await _client.SendAsync(request);
        var payload = await ApiResponseAssert.ReadCozeSuccessAsync(response);
        return long.Parse(payload.GetProperty("workflow_id").GetString() ?? throw new InvalidOperationException("missing workflow id"));
    }

    private async Task SaveWorkflowAsync(string accessToken, string csrfToken, long workflowId, string schema)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/workflow_api/save");
        request.Headers.Authorization = new("Bearer", accessToken);
        request.Headers.Add("X-Tenant-Id", IntegrationAuthHelper.DefaultTenantId);
        request.Headers.Add("X-Project-Id", "1");
        CsrfIdempotencyHelper.AddWriteSecurityHeaders(request, csrfToken);
        request.Content = JsonContent.Create(new
        {
            workflow_id = workflowId.ToString(),
            schema,
            space_id = "atlas-workflow",
            submit_commit_id = "itest-coze"
        });

        using var response = await _client.SendAsync(request);
        await ApiResponseAssert.ReadCozeSuccessAsync(response);
    }

    private static string BuildCozeNativeSchema(string entryVarName)
    {
        return """
        {
          "nodes": [
            {
              "id": "entry_1",
              "type": 1,
              "meta": { "position": { "x": 120, "y": 80 } },
              "data": {
                "nodeMeta": { "title": "开始" },
                "outputs": [{ "name": "__ENTRY_VAR__", "type": "string", "required": true }]
              }
            },
            {
              "id": "code_1",
              "type": 5,
              "meta": { "position": { "x": 360, "y": 80 } },
              "data": {
                "nodeMeta": { "title": "代码执行" },
                "inputs": {
                  "language": "javascript",
                  "code": "function main(args){ return { result: args.params.__ENTRY_VAR__ }; }",
                  "inputParameters": [{ "name": "__ENTRY_VAR__" }]
                },
                "outputs": [{ "name": "result", "type": "string" }]
              }
            },
            {
              "id": "exit_1",
              "type": 2,
              "meta": { "position": { "x": 660, "y": 80 } },
              "data": {
                "nodeMeta": { "title": "结束" },
                "inputs": {
                  "terminatePlan": "returnVariables",
                  "inputParameters": [
                    {
                      "name": "result",
                      "input": { "value": { "content": "{{code_1.result}}" } }
                    }
                  ]
                }
              }
            }
          ],
          "edges": [
            { "sourceNodeID": "entry_1", "targetNodeID": "code_1" },
            { "sourceNodeID": "code_1", "targetNodeID": "exit_1" }
          ]
        }
        """.Replace("__ENTRY_VAR__", entryVarName, StringComparison.Ordinal);
    }
}
