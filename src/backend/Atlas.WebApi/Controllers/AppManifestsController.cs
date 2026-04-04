using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Attributes;
using Atlas.WebApi.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/app-manifests")]
[DeprecatedApi("app-manifests v1 is in compatibility window", "/api/v2/application-catalogs")]
[Authorize]
[PlatformOnly]
public sealed class AppManifestsController : ControllerBase
{
    private readonly IAppManifestQueryService _queryService;
    private readonly IAppManifestCommandService _commandService;
    private readonly IAppReleaseCommandService _releaseCommandService;
    private readonly IAppDesignerSnapshotService _designerSnapshotService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AppManifestsController(
        IAppManifestQueryService queryService,
        IAppManifestCommandService commandService,
        IAppReleaseCommandService releaseCommandService,
        IAppDesignerSnapshotService designerSnapshotService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _queryService = queryService;
        _commandService = commandService;
        _releaseCommandService = releaseCommandService;
        _designerSnapshotService = designerSnapshotService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AppManifestResponse>>>> Query(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, request, cancellationToken: cancellationToken);
        return Ok(ApiResponse<PagedResult<AppManifestResponse>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<AppManifestResponse?>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<AppManifestResponse?>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] AppManifestCreateRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var id = await _commandService.CreateAsync(tenantId, currentUser.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] AppManifestUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.UpdateAsync(tenantId, currentUser.UserId, id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Archive(long id, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.ArchiveAsync(tenantId, currentUser.UserId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/releases")]
    public async Task<ActionResult<ApiResponse<object>>> CreateRelease(
        long id,
        [FromBody] Dictionary<string, string?>? request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        request ??= new Dictionary<string, string?>();
        request.TryGetValue("releaseNote", out var releaseNote);
        var tenantId = _tenantProvider.GetTenantId();
        var releaseId = await _releaseCommandService.CreateReleaseAsync(tenantId, currentUser.UserId, id, releaseNote, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = releaseId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/releases/{releaseId:long}/rollback")]
    public async Task<ActionResult<ApiResponse<object>>> Rollback(
        long id,
        long releaseId,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _releaseCommandService.RollbackAsync(tenantId, currentUser.UserId, id, releaseId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString(), ReleaseId = releaseId.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/workspace/overview")]
    public async Task<ActionResult<ApiResponse<WorkspaceOverviewResponse>>> GetWorkspaceOverview(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetWorkspaceOverviewAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<WorkspaceOverviewResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/workspace/pages")]
    public async Task<ActionResult<ApiResponse<PagedResult<object>>>> GetWorkspacePages(long id, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetWorkspacePagesAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<object>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/workspace/forms")]
    public async Task<ActionResult<ApiResponse<PagedResult<object>>>> GetWorkspaceForms(long id, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetWorkspaceFormsAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<object>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/workspace/flows")]
    public async Task<ActionResult<ApiResponse<PagedResult<object>>>> GetWorkspaceFlows(long id, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetWorkspaceFlowsAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<object>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/workspace/data")]
    public async Task<ActionResult<ApiResponse<PagedResult<object>>>> GetWorkspaceData(long id, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetWorkspaceDataAsync(tenantId, id, request, cancellationToken);
        return Ok(ApiResponse<PagedResult<object>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/workspace/permissions")]
    public async Task<ActionResult<ApiResponse<WorkspacePermissionResponse>>> GetWorkspacePermissions(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetWorkspacePermissionsAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<WorkspacePermissionResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/impact-analysis")]
    public ActionResult<ApiResponse<object>> GetImpactAnalysis(long id)
    {
        return StatusCode(501, ApiResponse<object>.Fail(
            "NOT_IMPLEMENTED",
            ApiResponseLocalizer.T(HttpContext, "AppManifestImpactAnalysisPending"),
            HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/workspace/designers/{type}/{itemId:long}")]
    public async Task<ActionResult<ApiResponse<DesignerSnapshotResponse>>> GetDesignerSnapshot(
        long id, string type, long itemId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _designerSnapshotService.GetSnapshotAsync(tenantId, id, type, itemId, cancellationToken);
        if (result is null)
        {
            return Ok(ApiResponse<DesignerSnapshotResponse?>.Ok(null, HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<DesignerSnapshotResponse>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/workspace/designers/{type}/{itemId:long}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DesignerSnapshotHistoryItem>>>> GetDesignerSnapshotHistory(
        long id, string type, long itemId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _designerSnapshotService.GetSnapshotHistoryAsync(tenantId, id, type, itemId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<DesignerSnapshotHistoryItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/workspace/designers/{type}/{itemId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> SaveDesignerSnapshot(
        long id, string type, long itemId,
        [FromBody] DesignerSnapshotSaveRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUser();
        if (currentUser is null)
        {
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, ApiResponseLocalizer.T(HttpContext, "Unauthorized"), HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        await _designerSnapshotService.SaveSnapshotAsync(
            tenantId, currentUser.UserId, id, type, itemId, request.SchemaJson, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Saved = true }, HttpContext.TraceIdentifier));
    }
}
