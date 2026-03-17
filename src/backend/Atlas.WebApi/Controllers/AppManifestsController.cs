using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/app-manifests")]
[DeprecatedApi("app-manifests v1 is in compatibility window", "/api/v2/application-catalogs")]
[Authorize]
public sealed class AppManifestsController : ControllerBase
{
    private readonly IAppManifestQueryService _queryService;
    private readonly IAppManifestCommandService _commandService;
    private readonly IAppReleaseCommandService _releaseCommandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AppManifestsController(
        IAppManifestQueryService queryService,
        IAppManifestCommandService commandService,
        IAppReleaseCommandService releaseCommandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _queryService = queryService;
        _commandService = commandService;
        _releaseCommandService = releaseCommandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AppManifestResponse>>>> Query(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.QueryAsync(tenantId, request, cancellationToken);
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
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
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
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
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
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
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
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
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
            return Unauthorized(ApiResponse<object>.Fail(ErrorCodes.Unauthorized, "未登录", HttpContext.TraceIdentifier));
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
        return Ok(ApiResponse<object>.Ok(new
        {
            ManifestId = id.ToString(),
            PageCount = 0,
            DataTableCount = 0,
            ActiveRuntimeRoutes = 0
        }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/workspace/designers/{type}/{itemId:long}")]
    public ActionResult<ApiResponse<object>> GetDesignerSnapshot(long id, string type, long itemId)
    {
        return Ok(ApiResponse<object>.Ok(new
        {
            ManifestId = id.ToString(),
            Type = type,
            ItemId = itemId.ToString(),
            SchemaJson = "{}"
        }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}/workspace/designers/{type}/{itemId:long}")]
    public ActionResult<ApiResponse<object>> SaveDesignerSnapshot(long id, string type, long itemId, [FromBody] Dictionary<string, object?> payload)
    {
        return Ok(ApiResponse<object>.Ok(new
        {
            ManifestId = id.ToString(),
            Type = type,
            ItemId = itemId.ToString(),
            Saved = true,
            PayloadSize = payload.Count
        }, HttpContext.TraceIdentifier));
    }
}
