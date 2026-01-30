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
[Route("users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserQueryService _userQueryService;
    private readonly IUserCommandService _userCommandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly Atlas.Core.Abstractions.IIdGenerator _idGenerator;
    private readonly IValidator<UserCreateRequest> _createValidator;
    private readonly IValidator<UserUpdateRequest> _updateValidator;

    public UsersController(
        IUserQueryService userQueryService,
        IUserCommandService userCommandService,
        ITenantProvider tenantProvider,
        Atlas.Core.Abstractions.IIdGenerator idGenerator,
        IValidator<UserCreateRequest> createValidator,
        IValidator<UserUpdateRequest> updateValidator)
    {
        _userQueryService = userQueryService;
        _userCommandService = userCommandService;
        _tenantProvider = tenantProvider;
        _idGenerator = idGenerator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.UsersView)]
    public async Task<ActionResult<ApiResponse<PagedResult<UserListItem>>>> Get(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _userQueryService.QueryUsersAsync(request, tenantId, cancellationToken);
        var payload = ApiResponse<PagedResult<UserListItem>>.Ok(result, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.UsersView)]
    public async Task<ActionResult<ApiResponse<UserDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _userQueryService.GetDetailAsync(id, tenantId, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<UserDetail>.Fail(ErrorCodes.NotFound, "User not found.", HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<UserDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.UsersCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] UserCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var id = _idGenerator.NextId();
        var createdId = await _userCommandService.CreateAsync(tenantId, request, id, cancellationToken);
        var payload = ApiResponse<object>.Ok(new { Id = createdId.ToString() }, HttpContext.TraceIdentifier);
        return Ok(payload);
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.UsersUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] UserUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await _userCommandService.UpdateAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/roles")]
    [Authorize(Policy = PermissionPolicies.UsersAssignRoles)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateRoles(
        long id,
        [FromBody] UserAssignRolesRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _userCommandService.UpdateRolesAsync(
            tenantId,
            id,
            request.RoleIds ?? Array.Empty<long>(),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/departments")]
    [Authorize(Policy = PermissionPolicies.UsersAssignDepartments)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateDepartments(
        long id,
        [FromBody] UserAssignDepartmentsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _userCommandService.UpdateDepartmentsAsync(
            tenantId,
            id,
            request.DepartmentIds ?? Array.Empty<long>(),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/positions")]
    [Authorize(Policy = PermissionPolicies.UsersAssignPositions)]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePositions(
        long id,
        [FromBody] UserAssignPositionsRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _userCommandService.UpdatePositionsAsync(
            tenantId,
            id,
            request.PositionIds ?? Array.Empty<long>(),
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.UsersDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _userCommandService.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
