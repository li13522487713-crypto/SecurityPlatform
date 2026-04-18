using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 治理 M-G05-C1（S9）：组织 CRUD。
/// </summary>
[ApiController]
[Route("api/v1/organizations")]
[Authorize]
public sealed class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public OrganizationsController(
        IOrganizationService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.TenantsView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OrganizationDto>>>> List(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var items = await _service.ListAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<OrganizationDto>>.Ok(items, HttpContext.TraceIdentifier));
    }

    [HttpGet("default")]
    [Authorize(Policy = PermissionPolicies.TenantsView)]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> GetDefault(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        var dto = await _service.GetOrCreateDefaultAsync(tenantId, actor.UserId, cancellationToken);
        return Ok(ApiResponse<OrganizationDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.TenantsCreate)]
    public async Task<ActionResult<ApiResponse<OrganizationDto>>> Create(
        [FromBody] OrganizationCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        var dto = await _service.CreateAsync(tenantId, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<OrganizationDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpPatch("{id:long}")]
    [Authorize(Policy = PermissionPolicies.TenantsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] OrganizationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        await _service.UpdateAsync(tenantId, id, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.TenantsDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}
