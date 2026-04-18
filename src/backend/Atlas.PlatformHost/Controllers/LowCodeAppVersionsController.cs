using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 设计态版本管理（M14 S14-1，**v1 前缀** /api/v1/lowcode/apps/{id}/versions）。
/// </summary>
[ApiController]
[Route("api/v1/lowcode/apps/{appId:long}/versions")]
public sealed class LowCodeAppVersionsController : ControllerBase
{
    private readonly IAppDefinitionQueryService _query;
    private readonly IAppVersioningService _versioning;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public LowCodeAppVersionsController(IAppDefinitionQueryService query, IAppVersioningService versioning, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _query = query;
        _versioning = versioning;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet("{from:long}/diff/{to:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<AppVersionDiffDto>>> Diff(long appId, long from, long to, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var r = await _versioning.DiffAsync(tenantId, appId, from, to, cancellationToken);
        return Ok(ApiResponse<AppVersionDiffDto>.Ok(r, HttpContext.TraceIdentifier));
    }

    [HttpPost("{versionId:long}/rollback")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppPublish)]
    public async Task<ActionResult<ApiResponse<object>>> Rollback(long appId, long versionId, [FromBody] AppVersionRollbackRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _versioning.RollbackAsync(tenantId, user.UserId, appId, versionId, request ?? new AppVersionRollbackRequest(null), cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}

[ApiController]
[Route("api/v1/lowcode/resources")]
public sealed class LowCodeResourceReferencesController : ControllerBase
{
    private readonly IResourceReferenceGuardService _guard;
    private readonly ITenantProvider _tenantProvider;

    public LowCodeResourceReferencesController(IResourceReferenceGuardService guard, ITenantProvider tenantProvider)
    {
        _guard = guard;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("{type}/{id}/references")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppResourceReferenceDto>>>> List(string type, string id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var list = await _guard.ListByResourceAsync(tenantId, type, id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AppResourceReferenceDto>>.Ok(list, HttpContext.TraceIdentifier));
    }
}

[ApiController]
[Route("api/v1/lowcode/faq")]
public sealed class LowCodeFaqController : ControllerBase
{
    private readonly IAppFaqService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public LowCodeFaqController(IAppFaqService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppFaqEntryDto>>>> Search([FromQuery] string? keyword, [FromQuery] int? pageIndex, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var list = await _service.SearchAsync(tenantId, keyword, pageIndex ?? 1, pageSize ?? 20, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AppFaqEntryDto>>.Ok(list, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Upsert([FromBody] AppFaqUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        var id = await _service.UpsertAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.DeleteAsync(tenantId, user.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/hit")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<AppFaqEntryDto?>>> Hit(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var r = await _service.HitAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<AppFaqEntryDto?>.Ok(r, HttpContext.TraceIdentifier));
    }
}
