using Atlas.Application.Identity.Models;
using Atlas.Application.Platform.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Helpers;

namespace Atlas.Presentation.Shared.Controllers.TenantAppV2;

/// <summary>应用级功能权限管理（独立于平台级权限）</summary>
[ApiController]
[Route("api/v2/tenant-app-instances/{appId:long}/permissions")]
[Authorize]
public sealed class TenantAppPermissionsController : ControllerBase
{
    private readonly IAppPermissionQueryService _queryService;
    private readonly IAppPermissionCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IValidator<PermissionCreateRequest> _createValidator;
    private readonly IValidator<PermissionUpdateRequest> _updateValidator;

    public TenantAppPermissionsController(
        IAppPermissionQueryService queryService,
        IAppPermissionCommandService commandService,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGen,
        IValidator<PermissionCreateRequest> createValidator,
        IValidator<PermissionUpdateRequest> updateValidator)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _idGen = idGen;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<PagedResult<PermissionListItem>>>> Get(
        long appId,
        [FromQuery] PermissionQueryRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, appId, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<PermissionListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesView)]
    public async Task<ActionResult<ApiResponse<PermissionDetail>>> GetById(
        long appId,
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var detail = await _queryService.GetByIdAsync(tenantId, appId, id, cancellationToken);
        if (detail is null)
        {
            return NotFound(ApiResponse<PermissionDetail>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AppScopedPermissionNotFound"), HttpContext.TraceIdentifier));
        }
        return Ok(ApiResponse<PermissionDetail>.Ok(detail, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        long appId,
        [FromBody] PermissionCreateRequest request,
        CancellationToken cancellationToken)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var tenantId = _tenantProvider.GetTenantId();
        var newId = _idGen.NextId();
        var createdId = await _commandService.CreateAsync(tenantId, appId, request, newId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = createdId.ToString(), appId = appId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long appId,
        long id,
        [FromBody] PermissionUpdateRequest request,
        CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, appId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString(), appId = appId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AppRolesUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long appId,
        long id,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.DeleteAsync(tenantId, appId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString(), appId = appId.ToString() }, HttpContext.TraceIdentifier));
    }
}



