using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Attributes;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/multi-agent-orchestrations")]
[Authorize]
[DeprecatedApi("multi-agent orchestrations v1 已进入兼容窗口", "/api/v1/team-agents", "2026-10-01")]
[Obsolete("Deprecated since April 1, 2026. 请迁移到 /api/v1/team-agents 相关接口。")]
[AppRuntimeOnly]
public sealed class MultiAgentOrchestrationsController : ControllerBase
{
    private readonly IMultiAgentOrchestrationService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<MultiAgentOrchestrationCreateRequest> _createValidator;
    private readonly IValidator<MultiAgentOrchestrationUpdateRequest> _updateValidator;
    private readonly IValidator<MultiAgentRunRequest> _runValidator;

    public MultiAgentOrchestrationsController(
        IMultiAgentOrchestrationService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<MultiAgentOrchestrationCreateRequest> createValidator,
        IValidator<MultiAgentOrchestrationUpdateRequest> updateValidator,
        IValidator<MultiAgentRunRequest> runValidator)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _runValidator = runValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<MultiAgentOrchestrationListItem>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetPagedAsync(
            tenantId,
            keyword,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<MultiAgentOrchestrationListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<MultiAgentOrchestrationDetail>>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<MultiAgentOrchestrationDetail>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "AiWorkflowNotFound"),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<MultiAgentOrchestrationDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AgentCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] MultiAgentOrchestrationCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var id = await _service.CreateAsync(tenantId, userId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] MultiAgentOrchestrationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/run")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<MultiAgentExecutionResult>>> Run(
        long id,
        [FromBody] MultiAgentRunRequest request,
        CancellationToken cancellationToken)
    {
        _runValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _service.RunAsync(tenantId, userId, id, request, cancellationToken);
        return Ok(ApiResponse<MultiAgentExecutionResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/stream")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task StreamRun(
        long id,
        [FromBody] MultiAgentRunRequest request,
        CancellationToken cancellationToken)
    {
        _runValidator.ValidateAndThrow(request);
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await foreach (var evt in _service.StreamRunAsync(tenantId, userId, id, request, cancellationToken))
        {
            await Response.WriteAsync($"event: {evt.EventType}\ndata: {evt.Data}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpGet("executions/{executionId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<MultiAgentExecutionResult>>> GetExecution(
        long executionId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetExecutionAsync(tenantId, executionId, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<MultiAgentExecutionResult>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "WorkflowInstanceNotFound"),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<MultiAgentExecutionResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
