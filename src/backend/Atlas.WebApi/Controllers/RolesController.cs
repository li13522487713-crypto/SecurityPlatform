using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Application.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Authorization;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("roles")]
public sealed class RolesController : ControllerBase
{
    private readonly IRoleQueryService _roleQueryService;
    private readonly IRoleCommandService _roleCommandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly Atlas.Core.Abstractions.IIdGenerator _idGenerator;
    private readonly IValidator<RoleCreateRequest> _createValidator;
    private readonly IValidator<RoleUpdateRequest> _updateValidator;

    public RolesController(
        IRoleQueryService roleQueryService,
        IRoleCommandService roleCommandService,
        ITenantProvider tenantProvider,
        Atlas.Core.Abstractions.IIdGenerator idGenerator,
        IValidator<RoleCreateRequest> createValidator,
        IValidator<RoleUpdateRequest> updateValidator)
    {
        _roleQueryService = roleQueryService;
        _roleCommandService = roleCommandService;
        _tenantProvider = tenantProvider;
        _idGenerator = idGenerator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.RolesView)]
    public async Task<ActionResult<ApiResponse<PagedResult<RoleListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _roleQueryService.QueryRolesAsync(request, tenantId, cancellationToken);
        return Ok(ApiResponse<PagedResult<RoleListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.RolesView)]
    public async Task<ActionResult<ApiResponse<RoleDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _roleQueryService.GetDetailAsync(id, tenantId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<RoleDetail>.Fail(ErrorCodes.NotFound, "Role not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<RoleDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.RolesCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] RoleCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = _idGenerator.NextId();
        var createdId = await _roleCommandService.CreateAsync(tenantId, request, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = createdId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.RolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] RoleUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _roleCommandService.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/permissions")]
    [Authorize(Policy = PermissionPolicies.RolesAssignPermissions)]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePermissions(
        long id,
        [FromBody] RoleAssignPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _roleCommandService.UpdatePermissionsAsync(
            tenantId,
            id,
            request.PermissionIds ?? Array.Empty<long>(),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/menus")]
    [Authorize(Policy = PermissionPolicies.RolesAssignMenus)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMenus(
        long id,
        [FromBody] RoleAssignMenusRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _roleCommandService.UpdateMenusAsync(
            tenantId,
            id,
            request.MenuIds ?? Array.Empty<long>(),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
