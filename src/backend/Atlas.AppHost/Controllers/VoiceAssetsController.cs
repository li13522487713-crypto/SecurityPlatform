using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/voice-assets")]
[Authorize]
public sealed class VoiceAssetsController : ControllerBase
{
    private readonly IVoiceAssetService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public VoiceAssetsController(
        IVoiceAssetService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<VoiceAssetCreatedDto>>> Create(
        [FromBody] VoiceAssetCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = _currentUserAccessor.GetCurrentUserOrThrow();
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.CreateAsync(tenantId, request, cancellationToken);
        return Ok(ApiResponse<VoiceAssetCreatedDto>.Ok(result, HttpContext.TraceIdentifier));
    }
}
