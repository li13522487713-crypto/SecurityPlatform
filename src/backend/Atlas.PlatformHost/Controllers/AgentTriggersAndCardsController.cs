using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 治理 M-G10-C1（S16）：Agent 触发器 CRUD。
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/agents/{agentId:long}/triggers")]
public sealed class AgentTriggersController : ControllerBase
{
    private readonly IAgentTriggerService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AgentTriggersController(IAgentTriggerService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AgentTriggerDto>>>> List(long agentId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var items = await _service.ListAsync(tenantId, agentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AgentTriggerDto>>.Ok(items, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<AgentTriggerDto>>> Create(long agentId, [FromBody] AgentTriggerUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        var dto = await _service.CreateAsync(tenantId, agentId, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<AgentTriggerDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpPatch("{triggerId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(long agentId, long triggerId, [FromBody] AgentTriggerUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateAsync(tenantId, agentId, triggerId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{triggerId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long agentId, long triggerId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, agentId, triggerId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}

/// <summary>
/// 治理 M-G10-C2（S16）：Agent 卡片 CRUD。
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/agents/{agentId:long}/cards")]
public sealed class AgentCardsController : ControllerBase
{
    private readonly IAgentCardService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public AgentCardsController(IAgentCardService service, ITenantProvider tenantProvider, ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.AgentView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AgentCardDto>>>> List(long agentId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var items = await _service.ListAsync(tenantId, agentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<AgentCardDto>>.Ok(items, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<AgentCardDto>>> Create(long agentId, [FromBody] AgentCardUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var actor = _currentUserAccessor.GetCurrentUserOrThrow();
        var dto = await _service.CreateAsync(tenantId, agentId, actor.UserId, request, cancellationToken);
        return Ok(ApiResponse<AgentCardDto>.Ok(dto, HttpContext.TraceIdentifier));
    }

    [HttpPatch("{cardId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(long agentId, long cardId, [FromBody] AgentCardUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.UpdateAsync(tenantId, agentId, cardId, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{cardId:long}")]
    [Authorize(Policy = PermissionPolicies.AgentUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long agentId, long cardId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.DeleteAsync(tenantId, agentId, cardId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }
}
