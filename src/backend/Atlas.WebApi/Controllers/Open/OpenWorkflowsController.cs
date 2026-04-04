using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Integration;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers.Open;

[ApiController]
[Route("api/v1/open/workflows")]
[Authorize(AuthenticationSchemes = $"{PatAuthenticationHandler.SchemeName},{OpenProjectAuthenticationHandler.SchemeName}")]
[AppRuntimeOnly]
public sealed class OpenWorkflowsController : ControllerBase
{
    private readonly IAiWorkflowExecutionService _executionService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IWebhookService _webhookService;
    private readonly ILogger<OpenWorkflowsController> _logger;

    public OpenWorkflowsController(
        IAiWorkflowExecutionService executionService,
        ITenantProvider tenantProvider,
        IWebhookService webhookService,
        ILogger<OpenWorkflowsController> logger)
    {
        _executionService = executionService;
        _tenantProvider = tenantProvider;
        _webhookService = webhookService;
        _logger = logger;
    }

    [HttpPost("{id:long}/run")]
    public async Task<ActionResult<ApiResponse<AiWorkflowExecutionRunResult>>> Run(
        long id,
        [FromBody] AiWorkflowExecutionRunRequest request,
        CancellationToken cancellationToken)
    {
        if (!OpenScopeHelper.HasScope(User, "open:workflow:run"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<AiWorkflowExecutionRunResult>.Fail(
                ErrorCodes.Forbidden,
                "PAT 缺少 open:workflow:run 权限",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _executionService.RunAsync(tenantId, id, request, cancellationToken);
        await TryDispatchWorkflowCompletedEventAsync(id, result, cancellationToken);
        return Ok(ApiResponse<AiWorkflowExecutionRunResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/stream")]
    public async Task StreamRun(
        long id,
        [FromBody] AiWorkflowExecutionRunRequest request,
        CancellationToken cancellationToken)
    {
        if (!OpenScopeHelper.HasScope(User, "open:workflow:run"))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                ErrorCodes.Forbidden,
                "PAT 缺少 open:workflow:run 权限",
                HttpContext.TraceIdentifier), cancellationToken);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var tenantId = _tenantProvider.GetTenantId();
        var runResult = await _executionService.RunAsync(tenantId, id, request, cancellationToken);
        await TryDispatchWorkflowCompletedEventAsync(id, runResult, cancellationToken);
        await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(runResult)}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
        await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    [HttpGet("executions/{executionId}")]
    public async Task<ActionResult<ApiResponse<AiWorkflowExecutionProgressDto>>> GetProgress(
        string executionId,
        CancellationToken cancellationToken)
    {
        if (!OpenScopeHelper.HasScope(User, "open:workflow:read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<AiWorkflowExecutionProgressDto>.Fail(
                ErrorCodes.Forbidden,
                "PAT 缺少 open:workflow:read 权限",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _executionService.GetProgressAsync(tenantId, executionId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AiWorkflowExecutionProgressDto>.Fail(
                ErrorCodes.NotFound,
                "执行记录不存在",
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AiWorkflowExecutionProgressDto>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{executionId}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AiWorkflowNodeHistoryItem>>>> GetHistory(
        string executionId,
        CancellationToken cancellationToken)
    {
        if (!OpenScopeHelper.HasScope(User, "open:workflow:read"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<IReadOnlyList<AiWorkflowNodeHistoryItem>>.Fail(
                ErrorCodes.Forbidden,
                "PAT 缺少 open:workflow:read 权限",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var result = await _executionService.GetNodeHistoryAsync(tenantId, executionId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AiWorkflowNodeHistoryItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    private async Task TryDispatchWorkflowCompletedEventAsync(
        long workflowId,
        AiWorkflowExecutionRunResult runResult,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                eventType = "workflow.completed",
                occurredAt = DateTimeOffset.UtcNow,
                workflowId,
                executionId = runResult.ExecutionId
            });
            await _webhookService.DispatchAsync("workflow.completed", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open workflow webhook dispatch failed.");
        }
    }
}
