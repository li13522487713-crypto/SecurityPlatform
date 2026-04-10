using Atlas.Application.Identity;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Presentation.Shared.Controllers.Identity;

public abstract class UsersControllerBase : ControllerBase
{
    protected readonly IUserQueryService UserQueryService;
    protected readonly IUserCommandService UserCommandService;
    protected readonly ITenantProvider TenantProvider;
    protected readonly Atlas.Core.Abstractions.IIdGeneratorAccessor IdGeneratorAccessor;
    protected readonly IValidator<UserCreateRequest> CreateValidator;
    protected readonly IValidator<UserUpdateRequest> UpdateValidator;

    protected UsersControllerBase(
        IUserQueryService userQueryService,
        IUserCommandService userCommandService,
        ITenantProvider tenantProvider,
        Atlas.Core.Abstractions.IIdGeneratorAccessor idGeneratorAccessor,
        IValidator<UserCreateRequest> createValidator,
        IValidator<UserUpdateRequest> updateValidator)
    {
        UserQueryService = userQueryService;
        UserCommandService = userCommandService;
        TenantProvider = tenantProvider;
        IdGeneratorAccessor = idGeneratorAccessor;
        CreateValidator = createValidator;
        UpdateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.UsersView)]
    public virtual async Task<ActionResult<ApiResponse<PagedResult<UserListItem>>>> Get(
        [FromQuery] UserQueryRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantProvider.GetTenantId();
        var result = await UserQueryService.QueryUsersAsync(request, tenantId, cancellationToken);
        var payload = ApiResponse<PagedResult<UserListItem>>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.UsersView)]
    public virtual async Task<ActionResult<ApiResponse<UserDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = TenantProvider.GetTenantId();
        var detail = await UserQueryService.GetDetailAsync(id, tenantId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<UserDetail>.Fail(ErrorCodes.NotFound, "User not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<UserDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.UsersCreate)]
    public virtual async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] UserCreateRequest request,
        CancellationToken cancellationToken)
    {
        CreateValidator.ValidateAndThrow(request);
        var tenantId = TenantProvider.GetTenantId();
        var id = IdGeneratorAccessor.NextId();
        var createdId = await UserCommandService.CreateAsync(tenantId, request, id, cancellationToken);
        var payload = ApiResponse<object>.Ok(new { Id = createdId.ToString() }, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.UsersUpdate)]
    public virtual async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] UserUpdateRequest request,
        CancellationToken cancellationToken)
    {
        UpdateValidator.ValidateAndThrow(request);
        var tenantId = TenantProvider.GetTenantId();
        await UserCommandService.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/roles")]
    [Authorize(Policy = PermissionPolicies.UsersAssignRoles)]
    public virtual async Task<ActionResult<ApiResponse<object>>> UpdateRoles(
        long id,
        [FromBody] UserAssignRolesRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantProvider.GetTenantId();
        await UserCommandService.UpdateRolesAsync(
            tenantId,
            id,
            request.RoleIds ?? Array.Empty<long>(),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/departments")]
    [Authorize(Policy = PermissionPolicies.UsersAssignDepartments)]
    public virtual async Task<ActionResult<ApiResponse<object>>> UpdateDepartments(
        long id,
        [FromBody] UserAssignDepartmentsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantProvider.GetTenantId();
        await UserCommandService.UpdateDepartmentsAsync(
            tenantId,
            id,
            request.DepartmentIds ?? Array.Empty<long>(),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/positions")]
    [Authorize(Policy = PermissionPolicies.UsersAssignPositions)]
    public virtual async Task<ActionResult<ApiResponse<object>>> UpdatePositions(
        long id,
        [FromBody] UserAssignPositionsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantProvider.GetTenantId();
        await UserCommandService.UpdatePositionsAsync(
            tenantId,
            id,
            request.PositionIds ?? Array.Empty<long>(),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.UsersDelete)]
    public virtual async Task<ActionResult<ApiResponse<object>>> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = TenantProvider.GetTenantId();
        await UserCommandService.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
