using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Filters;
using Atlas.Presentation.Shared.Helpers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/draft-agents")]
public sealed class DraftAgentsController : ControllerBase
{
    private readonly IAgentQueryService _queryService;
    private readonly IAgentCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<AgentCreateRequest> _createValidator;
    private readonly IValidator<AgentUpdateRequest> _updateValidator;
    private readonly IValidator<WorkflowBindingUpdateRequest> _workflowBindingValidator;

    public DraftAgentsController(
        IAgentQueryService queryService,
        IAgentCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<AgentCreateRequest> createValidator,
        IValidator<AgentUpdateRequest> updateValidator,
        IValidator<WorkflowBindingUpdateRequest> workflowBindingValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _workflowBindingValidator = workflowBindingValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AgentListItem>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetPagedAsync(
            tenantId,
            keyword,
            status,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<AgentListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<AgentDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AgentDetail>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AgentNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AgentDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AgentCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] AgentCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var id = await _commandService.CreateAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] AgentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/workflow-bindings")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<WorkflowBindingDto>>> BindWorkflow(
        long id,
        [FromBody] WorkflowBindingUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _workflowBindingValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.BindWorkflowAsync(tenantId, id, request.WorkflowId, cancellationToken);
        return Ok(ApiResponse<WorkflowBindingDto>.Ok(result, HttpContext.TraceIdentifier));
    }
}
