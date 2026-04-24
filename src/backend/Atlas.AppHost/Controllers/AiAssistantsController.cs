using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Helpers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/ai-assistants")]
[Authorize]
public sealed class AiAssistantsController : ControllerBase
{
    private readonly IAgentQueryService _queryService;
    private readonly IAgentCommandService _commandService;
    private readonly IAgentPublicationService _publicationService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<AgentCreateRequest> _createValidator;
    private readonly IValidator<AgentUpdateRequest> _updateValidator;
    private readonly IValidator<WorkflowBindingUpdateRequest> _workflowBindingValidator;
    private readonly IValidator<AgentDatabaseBindingInput> _databaseBindingValidator;
    private readonly IValidator<AgentPublicationPublishRequest> _publishValidator;
    private readonly IValidator<AgentPublicationRollbackRequest> _rollbackValidator;

    public AiAssistantsController(
        IAgentQueryService queryService,
        IAgentCommandService commandService,
        IAgentPublicationService publicationService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<AgentCreateRequest> createValidator,
        IValidator<AgentUpdateRequest> updateValidator,
        IValidator<WorkflowBindingUpdateRequest> workflowBindingValidator,
        IValidator<AgentDatabaseBindingInput> databaseBindingValidator,
        IValidator<AgentPublicationPublishRequest> publishValidator,
        IValidator<AgentPublicationRollbackRequest> rollbackValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _publicationService = publicationService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _workflowBindingValidator = workflowBindingValidator;
        _databaseBindingValidator = databaseBindingValidator;
        _publishValidator = publishValidator;
        _rollbackValidator = rollbackValidator;
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
            workspaceId: null,
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
            return NotFound(ApiResponse<AgentDetail>.Fail(
                ErrorCodes.NotFound,
                ApiResponseLocalizer.T(HttpContext, "AgentNotFound"),
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AgentDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiAppCreate)]
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

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, id, cancellationToken);
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

    [HttpPost("{id:long}/database-bindings")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AgentDatabaseBindingItem>>>> BindDatabase(
        long id,
        [FromBody] AgentDatabaseBindingInput request,
        CancellationToken cancellationToken)
    {
        _databaseBindingValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.BindDatabaseAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AgentDatabaseBindingItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}/database-bindings/{databaseId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AgentDatabaseBindingItem>>>> UnbindDatabase(
        long id,
        long databaseId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _commandService.UnbindDatabaseAsync(tenantId, id, databaseId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AgentDatabaseBindingItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/publications")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AgentPublicationListItem>>>> GetPublications(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _publicationService.GetByAgentAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AgentPublicationListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<AgentPublicationPublishResult>>> Publish(
        long id,
        [FromBody] AgentPublicationPublishRequest request,
        CancellationToken cancellationToken)
    {
        _publishValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var publisherUserId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _publicationService.PublishAsync(
            tenantId,
            id,
            publisherUserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<AgentPublicationPublishResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/rollback")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<AgentPublicationPublishResult>>> Rollback(
        long id,
        [FromBody] AgentPublicationRollbackRequest request,
        CancellationToken cancellationToken)
    {
        _rollbackValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var operatorUserId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _publicationService.RollbackAsync(
            tenantId,
            id,
            operatorUserId,
            request,
            cancellationToken);
        return Ok(ApiResponse<AgentPublicationPublishResult>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/embed-token")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<AgentEmbedTokenResult>>> RegenerateEmbedToken(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _publicationService.RegenerateEmbedTokenAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<AgentEmbedTokenResult>.Ok(result, HttpContext.TraceIdentifier));
    }
}
