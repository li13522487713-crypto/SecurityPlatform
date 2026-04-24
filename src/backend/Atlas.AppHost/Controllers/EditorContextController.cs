using Atlas.Application.Authorization;
using Atlas.Application.Platform.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

[ApiController]
[Route("api/v1/editor-context")]
[Authorize]
public sealed class EditorContextController : ControllerBase
{
    private readonly IResourceWorkspaceLookup _workspaceLookup;
    private readonly IWorkspacePortalService _workspacePortalService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public EditorContextController(
        IResourceWorkspaceLookup workspaceLookup,
        IWorkspacePortalService workspacePortalService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _workspaceLookup = workspaceLookup;
        _workspacePortalService = workspacePortalService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("workspace")]
    public async Task<ActionResult<ApiResponse<EditorContextWorkspaceResponse>>> ResolveWorkspace(
        [FromQuery] string resourceType,
        [FromQuery] string resourceId,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeResource(resourceType, resourceId, out var normalizedType, out var parsedResourceId))
        {
            return Ok(ApiResponse<EditorContextWorkspaceResponse>.Fail(
                "EDITOR_CONTEXT_INVALID_RESOURCE",
                "resourceType or resourceId is invalid.",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var workspaceId = await _workspaceLookup.ResolveWorkspaceIdAsync(
            tenantId,
            normalizedType,
            parsedResourceId,
            cancellationToken);

        if (workspaceId is not > 0)
        {
            return Ok(ApiResponse<EditorContextWorkspaceResponse>.Fail(
                "EDITOR_CONTEXT_WORKSPACE_UNRESOLVED",
                "The resource does not exist or is not bound to a workspace.",
                HttpContext.TraceIdentifier));
        }

        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var workspaceIdText = workspaceId.Value.ToString();

        // PlatformAdmin 拥有全租户资源访问权限，直接放行，避免 ListWorkspacesAsync
        // 因 tenantId 隔离或归档过滤而误判 FORBIDDEN。
        if (currentUser.IsPlatformAdmin)
        {
            return Ok(ApiResponse<EditorContextWorkspaceResponse>.Ok(
                new EditorContextWorkspaceResponse(normalizedType, parsedResourceId.ToString(), workspaceIdText),
                HttpContext.TraceIdentifier));
        }

        var workspaces = await _workspacePortalService.ListWorkspacesAsync(
            tenantId,
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);

        var hasAccess = workspaces.Any(item => string.Equals(item.Id, workspaceIdText, StringComparison.OrdinalIgnoreCase));
        if (!hasAccess)
        {
            return Ok(ApiResponse<EditorContextWorkspaceResponse>.Fail(
                "EDITOR_CONTEXT_WORKSPACE_FORBIDDEN",
                "The current user cannot access the workspace that owns this resource.",
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<EditorContextWorkspaceResponse>.Ok(
            new EditorContextWorkspaceResponse(normalizedType, parsedResourceId.ToString(), workspaceIdText),
            HttpContext.TraceIdentifier));
    }

    private static bool TryNormalizeResource(
        string? resourceType,
        string? resourceId,
        out string normalizedType,
        out long parsedResourceId)
    {
        normalizedType = string.Empty;
        parsedResourceId = 0;

        if (string.IsNullOrWhiteSpace(resourceType) || string.IsNullOrWhiteSpace(resourceId))
        {
            return false;
        }

        normalizedType = resourceType.Trim().ToLowerInvariant();
        if (normalizedType is not ("app" or "workflow" or "agent"))
        {
            return false;
        }

        return long.TryParse(resourceId.Trim(), out parsedResourceId) && parsedResourceId > 0;
    }
}

public sealed record EditorContextWorkspaceResponse(
    string ResourceType,
    string ResourceId,
    string WorkspaceId);
