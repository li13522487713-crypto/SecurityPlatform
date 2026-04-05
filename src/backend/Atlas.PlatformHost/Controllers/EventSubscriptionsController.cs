using Atlas.Application.Events;
using Atlas.Core.Models;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 事件订阅与路由配置 API
/// </summary>
[ApiController]
[Route("api/v1/event-subscriptions")]
public sealed class EventSubscriptionsController : ControllerBase
{
    private readonly IEventSubscriptionService _service;

    public EventSubscriptionsController(IEventSubscriptionService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.EventSubscriptionsView)]
    public async Task<ActionResult<ApiResponse<object>>> GetAll(CancellationToken cancellationToken = default)
    {
        var subs = await _service.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(subs, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.EventSubscriptionsView)]
    public async Task<ActionResult<ApiResponse<object>>> GetById(long id, CancellationToken cancellationToken = default)
    {
        var sub = await _service.GetByIdAsync(id, cancellationToken);
        if (sub is null)
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "WebhookSubscriptionNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<object>.Ok(sub, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.EventSubscriptionsCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateEventSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.EventSubscriptionsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] UpdateEventSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.EventSubscriptionsDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken = default)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}
