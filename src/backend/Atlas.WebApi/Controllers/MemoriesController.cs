using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/memories")]
[AppRuntimeOnly]
public sealed class MemoriesController : ControllerBase
{
    private readonly IAiMemoryService _memoryService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public MemoriesController(
        IAiMemoryService memoryService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _memoryService = memoryService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet("long-term")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<LongTermMemoryListItem>>>> GetLongTermMemories(
        [FromQuery] PagedRequest request,
        [FromQuery] long? agentId = null,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var result = await _memoryService.GetLongTermMemoriesAsync(
            tenantId,
            userId,
            agentId,
            keyword,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<LongTermMemoryListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpDelete("long-term/{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteLongTermMemory(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _memoryService.DeleteLongTermMemoryAsync(tenantId, userId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("long-term")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> ClearLongTermMemories(
        [FromQuery] long? agentId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var clearedCount = await _memoryService.ClearLongTermMemoriesAsync(
            tenantId,
            userId,
            agentId,
            cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Cleared = clearedCount }, HttpContext.TraceIdentifier));
    }
}
