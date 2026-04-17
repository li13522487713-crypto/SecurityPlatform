using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// Coze PRD 项目开发-文件夹（PRD 03-5.4）。
/// </summary>
[ApiController]
[Route("api/v1/workspaces/{workspaceId}/folders")]
[Authorize]
public sealed class WorkspaceFoldersController : ControllerBase
{
    private readonly IWorkspaceFolderService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public WorkspaceFoldersController(
        IWorkspaceFolderService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceView)]
    public async Task<ActionResult<ApiResponse<PagedResult<WorkspaceFolderListItem>>>> List(
        string workspaceId,
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var paged = new PagedRequest { PageIndex = pageIndex, PageSize = pageSize, Keyword = keyword };
        var result = await _service.ListAsync(tenantId, workspaceId, keyword, paged, cancellationToken);
        return Ok(ApiResponse<PagedResult<WorkspaceFolderListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        string workspaceId,
        [FromBody] WorkspaceFolderCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var folderId = await _service.CreateAsync(tenantId, workspaceId, currentUser, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = folderId, FolderId = folderId }, HttpContext.TraceIdentifier));
    }

    [HttpPatch("{folderId}")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        string workspaceId,
        string folderId,
        [FromBody] WorkspaceFolderUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateAsync(tenantId, workspaceId, folderId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{folderId}")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        string workspaceId,
        string folderId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, workspaceId, folderId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{folderId}/items")]
    [Authorize(Policy = PermissionPolicies.AiWorkspaceUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> MoveItem(
        string workspaceId,
        string folderId,
        [FromBody] WorkspaceFolderItemMoveRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.MoveItemAsync(tenantId, workspaceId, folderId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}
