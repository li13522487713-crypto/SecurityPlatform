using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Authorization;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Presentation.Shared.Controllers.Ai;

/// <summary>
/// 治理 M-G03-C7（S7）：通用资源协作者 CRUD API。
/// 路由：<c>/api/v1/workspaces/{workspaceId:long}/resources/{resourceType}/{resourceId:long}/collaborators</c>。
/// 写操作前置走 IResourceWriteGate（manage-permission 动作）；读操作走默认 [Authorize]。
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/workspaces/{workspaceId:long}/resources/{resourceType}/{resourceId:long}/collaborators")]
public sealed class ResourceCollaboratorsController : ControllerBase
{
    private readonly IResourceCollaboratorService _service;
    private readonly IResourceWriteGate _writeGate;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public ResourceCollaboratorsController(
        IResourceCollaboratorService service,
        IResourceWriteGate writeGate,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _writeGate = writeGate;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ResourceCollaboratorDto>>>> List(
        long workspaceId, string resourceType, long resourceId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var items = await _service.ListAsync(tenantId, workspaceId, resourceType, resourceId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ResourceCollaboratorDto>>.Ok(items, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Add(
        long workspaceId,
        string resourceType,
        long resourceId,
        [FromBody] ResourceCollaboratorAddRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        await _writeGate.GuardAsync(tenantId, workspaceId, resourceType, resourceId, "manage-permission", cancellationToken);
        await _service.AddAsync(tenantId, workspaceId, resourceType, resourceId, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpPatch("{userId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long workspaceId,
        string resourceType,
        long resourceId,
        long userId,
        [FromBody] ResourceCollaboratorUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        await _writeGate.GuardAsync(tenantId, workspaceId, resourceType, resourceId, "manage-permission", cancellationToken);
        await _service.UpdateAsync(tenantId, workspaceId, resourceType, resourceId, userId, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{userId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Remove(
        long workspaceId,
        string resourceType,
        long resourceId,
        long userId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        await _writeGate.GuardAsync(tenantId, workspaceId, resourceType, resourceId, "manage-permission", cancellationToken);
        await _service.RemoveAsync(tenantId, workspaceId, resourceType, resourceId, userId, actor.UserId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}
