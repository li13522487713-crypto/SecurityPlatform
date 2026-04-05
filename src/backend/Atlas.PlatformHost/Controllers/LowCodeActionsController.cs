using Atlas.Core.Models;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/lowcode-actions")]
[Authorize(Policy = PermissionPolicies.SystemAdmin)]
public sealed class LowCodeActionsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LowCodeActionsController> _logger;

    public LowCodeActionsController(
        IHttpClientFactory httpClientFactory,
        ILogger<LowCodeActionsController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private string TraceId => Activity.Current?.Id ?? HttpContext.TraceIdentifier;

    /// <summary>
    /// REST API proxy for low-code designer configured external API calls.
    /// </summary>
    [HttpPost("proxy/rest")]
    public async Task<ActionResult<ApiResponse<JsonElement>>> ProxyRestCall(
        [FromBody] RestProxyRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(ApiResponse<JsonElement>.Fail("VALIDATION_ERROR", "URL is required.", TraceId));
        }

        var allowedSchemes = new[] { "http", "https" };
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            !allowedSchemes.Contains(uri.Scheme))
        {
            return BadRequest(ApiResponse<JsonElement>.Fail("VALIDATION_ERROR", "Invalid URL.", TraceId));
        }

        try
        {
            var client = _httpClientFactory.CreateClient("LowCodeProxy");
            var httpMethod = new HttpMethod(request.Method?.ToUpperInvariant() ?? "GET");
            var httpRequest = new HttpRequestMessage(httpMethod, uri);

            if (request.Headers is not null)
            {
                foreach (var (key, value) in request.Headers)
                {
                    httpRequest.Headers.TryAddWithoutValidation(key, value);
                }
            }

            if (request.Body is not null && httpMethod != HttpMethod.Get && httpMethod != HttpMethod.Head)
            {
                httpRequest.Content = new StringContent(
                    request.Body.Value.GetRawText(),
                    System.Text.Encoding.UTF8,
                    "application/json");
            }

            var response = await client.SendAsync(httpRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            JsonElement result;
            try
            {
                result = JsonSerializer.Deserialize<JsonElement>(content);
            }
            catch
            {
                result = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(new { raw = content }));
            }

            return Ok(ApiResponse<JsonElement>.Ok(result, TraceId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "REST proxy call failed: {Url}", request.Url);
            return StatusCode(502, ApiResponse<JsonElement>.Fail("PROXY_ERROR", ex.Message, TraceId));
        }
    }

    /// <summary>
    /// Trigger a workflow instance from the low-code designer.
    /// </summary>
    [HttpPost("trigger-workflow")]
    public async Task<ActionResult<ApiResponse<WorkflowTriggerResult>>> TriggerWorkflow(
        [FromBody] WorkflowTriggerRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowDefinitionId))
        {
            return BadRequest(ApiResponse<WorkflowTriggerResult>.Fail("VALIDATION_ERROR", "WorkflowDefinitionId is required.", TraceId));
        }

        // Delegate to the workflow engine via HTTP to avoid tight coupling
        try
        {
            var client = _httpClientFactory.CreateClient();
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var startRequest = new HttpRequestMessage(HttpMethod.Post,
                $"{baseUrl}/api/v2/workflows/{request.WorkflowDefinitionId}/start");

            startRequest.Content = new StringContent(
                JsonSerializer.Serialize(new { data = request.InputData }),
                System.Text.Encoding.UTF8,
                "application/json");

            foreach (var header in Request.Headers.Where(h =>
                h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                h.Key.Equals("X-Tenant-Id", StringComparison.OrdinalIgnoreCase)))
            {
                startRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            var response = await client.SendAsync(startRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            return Ok(ApiResponse<WorkflowTriggerResult>.Ok(new WorkflowTriggerResult
            {
                WorkflowInstanceId = result.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                Status = "Started"
            }, TraceId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Workflow trigger failed: {DefinitionId}", request.WorkflowDefinitionId);
            return StatusCode(500, ApiResponse<WorkflowTriggerResult>.Fail("SERVER_ERROR", ex.Message, TraceId));
        }
    }

    /// <summary>
    /// Trigger an approval flow from the low-code designer.
    /// </summary>
    [HttpPost("trigger-approval")]
    public async Task<ActionResult<ApiResponse<ApprovalTriggerResult>>> TriggerApproval(
        [FromBody] ApprovalTriggerRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ApprovalFlowDefinitionId <= 0)
        {
            return BadRequest(ApiResponse<ApprovalTriggerResult>.Fail("VALIDATION_ERROR", "ApprovalFlowDefinitionId is required.", TraceId));
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var submitRequest = new HttpRequestMessage(HttpMethod.Post,
                $"{baseUrl}/api/v1/approval/instances");

            submitRequest.Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    flowDefinitionId = request.ApprovalFlowDefinitionId,
                    title = request.Title ?? "Low-code triggered approval",
                    formData = request.FormData
                }),
                System.Text.Encoding.UTF8,
                "application/json");

            foreach (var header in Request.Headers.Where(h =>
                h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                h.Key.Equals("X-Tenant-Id", StringComparison.OrdinalIgnoreCase)))
            {
                submitRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            var response = await client.SendAsync(submitRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<JsonElement>(content);

            return Ok(ApiResponse<ApprovalTriggerResult>.Ok(new ApprovalTriggerResult
            {
                InstanceId = result.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                Status = "Submitted"
            }, TraceId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Approval trigger failed: {DefinitionId}", request.ApprovalFlowDefinitionId);
            return StatusCode(500, ApiResponse<ApprovalTriggerResult>.Fail("SERVER_ERROR", ex.Message, TraceId));
        }
    }

    /// <summary>
    /// Execute a microflow (simplified workflow) defined in the low-code designer.
    /// </summary>
    [HttpPost("execute-microflow")]
    public async Task<ActionResult<ApiResponse<MicroflowExecutionResult>>> ExecuteMicroflow(
        [FromBody] MicroflowExecutionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MicroflowJson))
        {
            return BadRequest(ApiResponse<MicroflowExecutionResult>.Fail("VALIDATION_ERROR", "MicroflowJson is required.", TraceId));
        }

        try
        {
            var definition = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(request.MicroflowJson);
            var result = new MicroflowExecutionResult
            {
                Success = true,
                OutputData = request.InputData ?? new Dictionary<string, object>(),
                StepsExecuted = 0
            };

            if (definition.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
            {
                foreach (var step in steps.EnumerateArray())
                {
                    result.StepsExecuted++;
                    var stepType = step.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";

                    if (stepType == "api_call" && step.TryGetProperty("config", out var apiConfig))
                    {
                        var url = apiConfig.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            var client = _httpClientFactory.CreateClient("LowCodeProxy");
                            var method = apiConfig.TryGetProperty("method", out var m) ? m.GetString() ?? "GET" : "GET";
                            using var httpReq = new HttpRequestMessage(new HttpMethod(method), url);
                            foreach (var header in Request.Headers.Where(h =>
                                h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                                h.Key.Equals("X-Tenant-Id", StringComparison.OrdinalIgnoreCase)))
                            {
                                httpReq.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                            }
                            using var httpResp = await client.SendAsync(httpReq, cancellationToken);
                        }
                    }

                    if (stepType == "condition" && step.TryGetProperty("config", out var condConfig))
                    {
                        var field = condConfig.TryGetProperty("field", out var f) ? f.GetString() ?? "" : "";
                        var expectedValue = condConfig.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";
                        if (request.InputData != null && request.InputData.TryGetValue(field, out var actual))
                        {
                            if (actual?.ToString() != expectedValue)
                            {
                                continue;
                            }
                        }
                    }
                }
            }

            return Ok(ApiResponse<MicroflowExecutionResult>.Ok(result, TraceId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Microflow execution failed");
            return StatusCode(500, ApiResponse<MicroflowExecutionResult>.Fail("SERVER_ERROR", ex.Message, TraceId));
        }
    }
}

public sealed class RestProxyRequest
{
    public string Url { get; set; } = "";
    public string? Method { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public JsonElement? Body { get; set; }
}

public sealed class WorkflowTriggerRequest
{
    public string WorkflowDefinitionId { get; set; } = "";
    public Dictionary<string, object>? InputData { get; set; }
}

public sealed class WorkflowTriggerResult
{
    public string WorkflowInstanceId { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class ApprovalTriggerRequest
{
    public long ApprovalFlowDefinitionId { get; set; }
    public string? Title { get; set; }
    public Dictionary<string, object>? FormData { get; set; }
}

public sealed class ApprovalTriggerResult
{
    public string InstanceId { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class MicroflowExecutionRequest
{
    public string MicroflowJson { get; set; } = "";
    public Dictionary<string, object>? InputData { get; set; }
}

public sealed class MicroflowExecutionResult
{
    public bool Success { get; set; }
    public int StepsExecuted { get; set; }
    public Dictionary<string, object>? OutputData { get; set; }
}
