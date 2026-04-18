using Atlas.Application.LowCode.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Services.LowCode;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 组件注册表控制器（M06 S06-2，**设计态前缀** /api/v1/lowcode/components）。
/// </summary>
[ApiController]
[Route("api/v1/lowcode/components")]
public sealed class LowCodeComponentsController : ControllerBase
{
    private readonly ILowCodeComponentManifestService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUser;

    public LowCodeComponentsController(ILowCodeComponentManifestService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUser)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUser = currentUser;
    }

    [HttpGet("registry")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppView)]
    public async Task<ActionResult<ApiResponse<ComponentRegistryDto>>> Registry([FromQuery] string? renderer, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var r = await _service.GetRegistryAsync(tenantId, renderer, cancellationToken);
        return Ok(ApiResponse<ComponentRegistryDto>.Ok(r, HttpContext.TraceIdentifier));
    }

    [HttpPost("overrides")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Upsert([FromBody] ComponentOverrideUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.UpsertOverrideAsync(tenantId, user.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpDelete("overrides/{type}")]
    [Authorize(Policy = PermissionPolicies.LowcodeAppUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string type, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var user = _currentUser.GetCurrentUserOrThrow();
        await _service.DeleteOverrideAsync(tenantId, user.UserId, type, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}
