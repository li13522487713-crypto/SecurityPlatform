using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Enums;
using Atlas.WebApi.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/batch-jobs")]
public sealed class BatchJobsController : ControllerBase
{
    private readonly IBatchJobQueryService _queryService;
    private readonly IBatchJobCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<BatchJobCreateRequest> _createValidator;
    private readonly IValidator<BatchJobUpdateRequest> _updateValidator;

    public BatchJobsController(
        IBatchJobQueryService queryService,
        IBatchJobCommandService commandService,
        ITenantProvider tenantProvider,
        IValidator<BatchJobCreateRequest> createValidator,
        IValidator<BatchJobUpdateRequest> updateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<BatchJobDefinitionListItem>>>> GetJobs(
        [FromQuery] PagedRequest request,
        [FromQuery] BatchJobStatus? status,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryJobsAsync(request, status, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<BatchJobDefinitionListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<BatchJobDefinitionResponse>>> GetJob(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetJobByIdAsync(id, tenantId, cancellationToken);
        if (result is null)
            return NotFound(ApiResponse<BatchJobDefinitionResponse>.Fail(ErrorCodes.NotFound, "批处理任务不存在", HttpContext.TraceIdentifier));
        return Ok(ApiResponse<BatchJobDefinitionResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] BatchJobCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = User.FindFirst("sub")?.Value ?? "system";
        var id = await _commandService.CreateAsync(request, tenantId, userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] BatchJobUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(id, request, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/activate")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Activate(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.ActivateAsync(id, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/pause")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Pause(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.PauseAsync(id, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/archive")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Archive(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.ArchiveAsync(id, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/trigger")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Trigger(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = User.FindFirst("sub")?.Value ?? "system";
        var executionId = await _commandService.TriggerAsync(id, tenantId, userId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { ExecutionId = executionId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id}/executions")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<BatchJobExecutionListItem>>>> GetExecutions(
        long id,
        [FromQuery] PagedRequest request,
        [FromQuery] JobExecutionStatus? status,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryExecutionsAsync(id, request, status, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<BatchJobExecutionListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{executionId}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<BatchJobExecutionResponse>>> GetExecution(
        long executionId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetExecutionByIdAsync(executionId, tenantId, cancellationToken);
        if (result is null)
            return NotFound(ApiResponse<BatchJobExecutionResponse>.Fail(ErrorCodes.NotFound, "执行实例不存在", HttpContext.TraceIdentifier));
        return Ok(ApiResponse<BatchJobExecutionResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("executions/{executionId}/shards")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ShardExecutionResponse>>>> GetShards(
        long executionId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetShardsByExecutionIdAsync(executionId, tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ShardExecutionResponse>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("executions/{executionId}/cancel")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> CancelExecution(long executionId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.CancelExecutionAsync(executionId, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}
