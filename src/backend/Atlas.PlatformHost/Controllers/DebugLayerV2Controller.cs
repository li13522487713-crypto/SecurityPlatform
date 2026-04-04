using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v2/debug-layer")]
[Authorize]
public sealed class DebugLayerV2Controller : ControllerBase
{
    private readonly IDebugLayerQueryService _queryService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly IProjectContextAccessor _projectContextAccessor;

    public DebugLayerV2Controller(
        IDebugLayerQueryService queryService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IAppContextAccessor appContextAccessor,
        IProjectContextAccessor projectContextAccessor)
    {
        _queryService = queryService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _appContextAccessor = appContextAccessor;
        _projectContextAccessor = projectContextAccessor;
    }

    [HttpGet("embed-metadata")]
    [Authorize(Policy = PermissionPolicies.DebugView)]
    public async Task<ActionResult<ApiResponse<DebugLayerEmbedMetadata>>> GetEmbedMetadata(
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<DebugLayerEmbedMetadata>.Fail(ErrorCodes.Unauthorized, "Unauthorized.", HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var appId = _appContextAccessor.GetAppId();
        var projectContext = _projectContextAccessor.GetCurrent();
        var result = await _queryService.GetEmbedMetadataAsync(
            tenantId,
            currentUser.UserId,
            appId,
            projectContext.ProjectId,
            projectContext.IsEnabled,
            cancellationToken);
        return Ok(ApiResponse<DebugLayerEmbedMetadata>.Ok(result, HttpContext.TraceIdentifier));
    }
}
