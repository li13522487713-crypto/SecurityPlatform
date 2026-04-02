using Atlas.Application.LogicFlow.Flows.Abstractions;
using Atlas.Application.LogicFlow.Flows.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LogicFlow.Flows;
using Atlas.WebApi.Authorization;
using Atlas.WebApi.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/logic-flows")]
[Authorize(Policy = PermissionPolicies.SystemAdmin)]
public sealed class LogicFlowsController : ControllerBase
{
    private readonly ILogicFlowQueryService _queryService;
    private readonly ILogicFlowCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IValidator<LogicFlowCreateRequest> _createValidator;
    private readonly IValidator<LogicFlowUpdateRequest> _updateValidator;

    public LogicFlowsController(
        ILogicFlowQueryService queryService,
        ILogicFlowCommandService commandService,
        ITenantProvider tenantProvider,
        IValidator<LogicFlowCreateRequest> createValidator,
        IValidator<LogicFlowUpdateRequest> updateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<LogicFlowListItem>>>> GetFlows(
        [FromQuery] PagedRequest request,
        [FromQuery] FlowStatus? status,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(request, status, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<LogicFlowListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<LogicFlowDetailResponse>>> GetFlow(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(id, tenantId, cancellationToken);
        if (result is null)
            return NotFound(ApiResponse<LogicFlowDetailResponse>.Fail(ErrorCodes.NotFound, "逻辑流不存在", HttpContext.TraceIdentifier));
        return Ok(ApiResponse<LogicFlowDetailResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] LogicFlowWriteRequest body,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(body.Flow);
        var tenantId = _tenantProvider.GetTenantId();
        var userId = User.FindFirst("sub")?.Value ?? "system";
        var id = await _commandService.CreateAsync(
            body.Flow,
            body.Nodes ?? [],
            body.Edges ?? [],
            tenantId,
            userId,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] LogicFlowUpdateWriteRequest body,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(body.Flow);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(
            id,
            body.Flow,
            body.Nodes ?? [],
            body.Edges ?? [],
            tenantId,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    public async Task<ActionResult<ApiResponse<object>>> Publish(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.PublishAsync(id, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/archive")]
    public async Task<ActionResult<ApiResponse<object>>> Archive(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.ArchiveAsync(id, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(id, tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}
