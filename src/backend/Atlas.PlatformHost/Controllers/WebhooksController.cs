using Atlas.Application.Integration;
using Atlas.Core.Models;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// Webhook 订阅管理 API
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
public sealed class WebhooksController : ControllerBase
{
    private readonly IWebhookService _webhookService;

    public WebhooksController(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    /// <summary>获取所有 Webhook 订阅</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.WebhooksView)]
    public async Task<ActionResult<ApiResponse<object>>> GetAll(CancellationToken cancellationToken = default)
    {
        var subs = await _webhookService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(subs, HttpContext.TraceIdentifier));
    }

    /// <summary>获取指定订阅</summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = PermissionPolicies.WebhooksView)]
    public async Task<ActionResult<ApiResponse<object>>> GetById(long id, CancellationToken cancellationToken = default)
    {
        var sub = await _webhookService.GetByIdAsync(id, cancellationToken);
        if (sub is null)
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", ApiResponseLocalizer.T(HttpContext, "WebhookSubscriptionNotFound"), HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<object>.Ok(sub, HttpContext.TraceIdentifier));
    }

    /// <summary>创建 Webhook 订阅</summary>
    [HttpPost]
    [Authorize(Policy = PermissionPolicies.WebhooksCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = await _webhookService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, HttpContext.TraceIdentifier));
    }

    /// <summary>更新 Webhook 订阅</summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.WebhooksUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] UpdateWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        await _webhookService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>删除 Webhook 订阅</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.WebhooksDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(long id, CancellationToken cancellationToken = default)
    {
        await _webhookService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>获取投递记录</summary>
    [HttpGet("{id:long}/deliveries")]
    [Authorize(Policy = PermissionPolicies.WebhooksView)]
    public async Task<ActionResult<ApiResponse<object>>> GetDeliveries(
        long id,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var logs = await _webhookService.GetDeliveriesAsync(id, pageSize, cancellationToken);
        return Ok(ApiResponse<object>.Ok(logs, HttpContext.TraceIdentifier));
    }

    /// <summary>测试投递</summary>
    [HttpPost("{id:long}/test")]
    [Authorize(Policy = PermissionPolicies.WebhooksTest)]
    public async Task<ActionResult<ApiResponse<object>>> Test(long id, CancellationToken cancellationToken = default)
    {
        await _webhookService.TestDeliveryAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}
