using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Authorization;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Atlas.Presentation.Shared.Helpers;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Presentation.Shared.Controllers.Ai;

[ApiController]
[Route("api/v1/agents")]
public sealed class AgentsController : ControllerBase
{
    private const string ResourceType = "agent";
    private readonly IAgentQueryService _queryService;
    private readonly IAgentCommandService _commandService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IValidator<AgentCreateRequest> _createValidator;
    private readonly IValidator<AgentUpdateRequest> _updateValidator;
    private readonly IResourceWriteGate _writeGate;

    public AgentsController(
        IAgentQueryService queryService,
        IAgentCommandService commandService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor,
        IValidator<AgentCreateRequest> createValidator,
        IValidator<AgentUpdateRequest> updateValidator,
        IResourceWriteGate writeGate)
    {
        _queryService = queryService;
        _commandService = commandService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _writeGate = writeGate;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<PagedResult<AgentListItem>>>> GetPaged(
        [FromQuery] PagedRequest request,
        [FromQuery] string? keyword = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetPagedAsync(
            tenantId,
            keyword,
            status,
            workspaceId: null,
            request.PageIndex,
            request.PageSize,
            cancellationToken);
        return Ok(ApiResponse<PagedResult<AgentListItem>>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<AgentDetail>>> GetById(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _queryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(ApiResponse<AgentDetail>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "AgentNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AgentDetail>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AiAppCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] AgentCreateRequest request,
        CancellationToken cancellationToken)
    {
        _createValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        // 治理 M-G03-C3 (S6)：写前 ACL（按 workspace edit 权限），仅在请求带 workspaceId 时生效。
        if (request.WorkspaceId is > 0)
        {
            await _writeGate.GuardAsync(tenantId, request.WorkspaceId.Value, ResourceType, resourceId: null, action: "edit", cancellationToken);
        }
        var id = await _commandService.CreateAsync(tenantId, currentUser.UserId, request, cancellationToken);
        if (request.WorkspaceId is > 0)
        {
            await _writeGate.InvalidateAsync(tenantId, ResourceType, id, cancellationToken);
        }
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] AgentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        _updateValidator.ValidateAndThrow(request);
        var tenantId = _tenantProvider.GetTenantId();
        await EnsureGuardedAsync(tenantId, id, "edit", cancellationToken);
        await _commandService.UpdateAsync(tenantId, id, request, cancellationToken);
        await _writeGate.InvalidateAsync(tenantId, ResourceType, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.AgentDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await EnsureGuardedAsync(tenantId, id, "delete", cancellationToken);
        await _commandService.DeleteAsync(tenantId, id, cancellationToken);
        await _writeGate.InvalidateAsync(tenantId, ResourceType, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/duplicate")]
    [Authorize(Policy = PermissionPolicies.AiAppCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Duplicate(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        // 复制操作：要求对源 agent 的 view 权限（同 workspace 才允许复制）。
        await EnsureGuardedAsync(tenantId, id, "view", cancellationToken);
        var duplicateId = await _commandService.DuplicateAsync(tenantId, currentUser.UserId, id, cancellationToken);
        await _writeGate.InvalidateAsync(tenantId, ResourceType, duplicateId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = duplicateId.ToString() }, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 治理 M-G03-C3 (S6) 帮助方法：根据 agent 的当前 workspaceId 走 IResourceWriteGate；
    /// 老数据 workspaceId 为空时跳过守卫（向后兼容），仍走 PDP 默认 [Authorize] 策略。
    /// </summary>
    private async Task EnsureGuardedAsync(TenantId tenantId, long agentId, string action, CancellationToken cancellationToken)
    {
        var detail = await _queryService.GetByIdAsync(tenantId, agentId, cancellationToken);
        if (detail is null)
        {
            return; // 留给 service 自身抛 NotFound，保持错误语义一致
        }
        if (detail.WorkspaceId is > 0)
        {
            await _writeGate.GuardAsync(tenantId, detail.WorkspaceId.Value, ResourceType, agentId, action, cancellationToken);
        }
    }

    [HttpPost("{id:long}/publish")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(long id, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _commandService.PublishAsync(tenantId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
