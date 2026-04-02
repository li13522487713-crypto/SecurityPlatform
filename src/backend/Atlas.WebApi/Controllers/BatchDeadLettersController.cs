using Atlas.Application.BatchProcess.Abstractions;
using Atlas.Application.BatchProcess.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.BatchProcess.Enums;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/batch-dead-letters")]
public sealed class BatchDeadLettersController : ControllerBase
{
    private readonly IBatchDeadLetterQueryService _queryService;
    private readonly IBatchDeadLetterCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;

    public BatchDeadLettersController(
        IBatchDeadLetterQueryService queryService,
        IBatchDeadLetterCommandService commandService,
        ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<PagedResult<BatchDeadLetterListItem>>>> Get(
        [FromQuery] PagedRequest request,
        [FromQuery] long? jobExecutionId,
        [FromQuery] DeadLetterStatus? status,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(jobExecutionId, status, request, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<BatchDeadLetterListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<BatchDeadLetterResponse>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(id, tenantId, cancellationToken);
        if (result is null)
            return NotFound(ApiResponse<BatchDeadLetterResponse>.Fail(ErrorCodes.NotFound, "死信记录不存在", HttpContext.TraceIdentifier));
        return Ok(ApiResponse<BatchDeadLetterResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/retry")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Retry(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.RetryAsync(id, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("batch-retry")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> RetryBatch(
        [FromBody] BatchDeadLetterIdsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.RetryBatchAsync(request.Ids, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id}/abandon")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> Abandon(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.AbandonAsync(id, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("batch-abandon")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> AbandonBatch(
        [FromBody] BatchDeadLetterIdsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.AbandonBatchAsync(request.Ids, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}

public sealed class BatchDeadLetterIdsRequest
{
    public IReadOnlyList<long> Ids { get; set; } = [];
}
