using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

/// <summary>应用级部门管理</summary>
[ApiController]
[Route("api/v2/tenant-app-instances/{appId:long}/departments")]
[Authorize]
[PlatformOnly]
public sealed class TenantAppDepartmentsController : ControllerBase
{
    private readonly IAppOrgQueryService _queryService;
    private readonly IAppOrgCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly Atlas.Core.Abstractions.IIdGeneratorAccessor _idGen;

    public TenantAppDepartmentsController(
        IAppOrgQueryService queryService,
        IAppOrgCommandService commandService,
        ITenantProvider tenantProvider,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGen)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _idGen = idGen;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AppDepartmentListItem>>>> Get(
        long appId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryDepartmentsAsync(tenantId, appId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<AppDepartmentListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("all")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppDepartmentListItem>>>> GetAll(
        long appId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetAllDepartmentsAsync(tenantId, appId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AppDepartmentListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<AppDepartmentDetail>>> GetById(
        long appId, long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var item = await _queryService.GetDepartmentByIdAsync(tenantId, appId, id, cancellationToken);
        if (item is null)
            return NotFound(ApiResponse<AppDepartmentDetail>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AppOrgDepartmentNotFound"), HttpContext.TraceIdentifier));
        return Ok(ApiResponse<AppDepartmentDetail>.Ok(item, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        long appId, [FromBody] AppDepartmentCreateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateDepartmentAsync(tenantId, appId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long appId, long id, [FromBody] AppDepartmentUpdateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateDepartmentAsync(tenantId, appId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long appId, long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteDepartmentAsync(tenantId, appId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}

/// <summary>应用级职位管理</summary>
[ApiController]
[Route("api/v2/tenant-app-instances/{appId:long}/positions")]
[Authorize]
[PlatformOnly]
public sealed class TenantAppPositionsController : ControllerBase
{
    private readonly IAppOrgQueryService _queryService;
    private readonly IAppOrgCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;

    public TenantAppPositionsController(
        IAppOrgQueryService queryService,
        IAppOrgCommandService commandService,
        ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AppPositionListItem>>>> Get(
        long appId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryPositionsAsync(tenantId, appId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<AppPositionListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("all")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppPositionListItem>>>> GetAll(
        long appId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetAllPositionsAsync(tenantId, appId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AppPositionListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<AppPositionDetail>>> GetById(
        long appId, long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var item = await _queryService.GetPositionByIdAsync(tenantId, appId, id, cancellationToken);
        if (item is null)
            return NotFound(ApiResponse<AppPositionDetail>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AppOrgPositionNotFound"), HttpContext.TraceIdentifier));
        return Ok(ApiResponse<AppPositionDetail>.Ok(item, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        long appId, [FromBody] AppPositionCreateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreatePositionAsync(tenantId, appId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long appId, long id, [FromBody] AppPositionUpdateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdatePositionAsync(tenantId, appId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long appId, long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeletePositionAsync(tenantId, appId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}

/// <summary>应用级项目管理</summary>
[ApiController]
[Route("api/v2/tenant-app-instances/{appId:long}/projects")]
[Authorize]
[PlatformOnly]
public sealed class TenantAppProjectsController : ControllerBase
{
    private readonly IAppOrgQueryService _queryService;
    private readonly IAppOrgCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;

    public TenantAppProjectsController(
        IAppOrgQueryService queryService,
        IAppOrgCommandService commandService,
        ITenantProvider tenantProvider)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AppProjectListItem>>>> Get(
        long appId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryProjectsAsync(tenantId, appId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<AppProjectListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("all")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppProjectListItem>>>> GetAll(
        long appId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetAllProjectsAsync(tenantId, appId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AppProjectListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<AppProjectDetail>>> GetById(
        long appId, long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var item = await _queryService.GetProjectByIdAsync(tenantId, appId, id, cancellationToken);
        if (item is null)
            return NotFound(ApiResponse<AppProjectDetail>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AppOrgProjectNotFound"), HttpContext.TraceIdentifier));
        return Ok(ApiResponse<AppProjectDetail>.Ok(item, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        long appId, [FromBody] AppProjectCreateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateProjectAsync(tenantId, appId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long appId, long id, [FromBody] AppProjectUpdateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateProjectAsync(tenantId, appId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long appId, long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteProjectAsync(tenantId, appId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
