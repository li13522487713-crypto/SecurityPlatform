using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v2/debug-layer")]
[Authorize]
public sealed class DebugLayerV2Controller : ControllerBase
{
    private readonly IDebugLayerQueryService _queryService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly IProjectContextAccessor _projectContextAccessor;

    public DebugLayerV2Controller(
        IDebugLayerQueryService queryService,
        ITenantProvider tenantProvider,
        IAppContextAccessor appContextAccessor,
        IProjectContextAccessor projectContextAccessor)
    {
        _queryService = queryService;
        _tenantProvider = tenantProvider;
        _appContextAccessor = appContextAccessor;
        _projectContextAccessor = projectContextAccessor;
    }

    [HttpGet("embed-metadata")]
    [Authorize(Policy = PermissionPolicies.AppsView)]
    public async Task<ActionResult<ApiResponse<DebugLayerEmbedMetadata>>> GetEmbedMetadata(
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var appId = _appContextAccessor.GetAppId();
        var projectContext = _projectContextAccessor.GetCurrent();
        var result = await _queryService.GetEmbedMetadataAsync(
            tenantId,
            appId,
            projectContext.ProjectId,
            projectContext.IsEnabled,
            cancellationToken);
        return Ok(ApiResponse<DebugLayerEmbedMetadata>.Ok(result, HttpContext.TraceIdentifier));
    }
}
