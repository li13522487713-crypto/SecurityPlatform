using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Authorization;
using Atlas.Core.Identity;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/projects")]
[PlatformOnly]
public sealed class ProjectsController : ControllerBase
{
    private readonly IProjectQueryService _queryService;
    private readonly IProjectCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly Atlas.Core.Abstractions.IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IValidator<ProjectCreateRequest> _createValidator;
    private readonly IValidator<ProjectUpdateRequest> _updateValidator;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ProjectsController(
        IProjectQueryService queryService,
        IProjectCommandService commandService,
        ITenantProvider tenantProvider,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGeneratorAccessor,
        IValidator<ProjectCreateRequest> createValidator,
        IValidator<ProjectUpdateRequest> updateValidator,
        ICurrentUserAccessor currentUserAccessor)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _idGeneratorAccessor = idGeneratorAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.ProjectsView)]
    public async Task<ActionResult<ApiResponse<PagedResult<ProjectListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryProjectsAsync(request, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<ProjectListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("my")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ProjectListItem>>>> GetMyProjects(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<IReadOnlyList<ProjectListItem>>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var items = await _queryService.QueryMyProjectsAsync(tenantId, currentUser.UserId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ProjectListItem>>.Ok(items, HttpContext.TraceIdentifier));
    }

    [HttpGet("my/paged")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PagedResult<ProjectListItem>>>> GetMyProjectsPaged(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<PagedResult<ProjectListItem>>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var result = await _queryService.QueryMyProjectsPagedAsync(request, tenantId, currentUser.UserId, cancellationToken);
        return Ok(ApiResponse<PagedResult<ProjectListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ProjectsView)]
    public async Task<ActionResult<ApiResponse<ProjectDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetDetailAsync(id, tenantId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<ProjectDetail>.Fail(ErrorCodes.NotFound, "Project not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<ProjectDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.ProjectsCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] ProjectCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = _idGeneratorAccessor.NextId();
        var createdId = await _commandService.CreateAsync(tenantId, request, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = createdId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ProjectsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] ProjectUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/users")]
    [Authorize(Policy = PermissionPolicies.ProjectsAssignUsers)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateUsers(
        long id,
        [FromBody] ProjectAssignUsersRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateUsersAsync(tenantId, id, request.UserIds ?? Array.Empty<long>(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/departments")]
    [Authorize(Policy = PermissionPolicies.ProjectsAssignDepartments)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateDepartments(
        long id,
        [FromBody] ProjectAssignDepartmentsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateDepartmentsAsync(tenantId, id, request.DepartmentIds ?? Array.Empty<long>(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/positions")]
    [Authorize(Policy = PermissionPolicies.ProjectsAssignPositions)]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePositions(
        long id,
        [FromBody] ProjectAssignPositionsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdatePositionsAsync(tenantId, id, request.PositionIds ?? Array.Empty<long>(), cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.ProjectsDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
