using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// Coze PRD 通用管理（PRD 02-7.12）。
/// </summary>
[ApiController]
[Route("api/v1/platform/general")]
[Authorize]
public sealed class PlatformGeneralController : ControllerBase
{
    private readonly IPlatformGeneralService _service;
    private readonly ITenantProvider _tenantProvider;

    public PlatformGeneralController(IPlatformGeneralService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("notices")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PlatformNoticeDto>>>> ListNotices(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.ListNoticesAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PlatformNoticeDto>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("branding")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<PlatformBrandingDto>>> GetBranding(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetBrandingAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<PlatformBrandingDto>.Ok(result, HttpContext.TraceIdentifier));
    }
}
