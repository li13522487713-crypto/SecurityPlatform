using Atlas.Application.Integration;
using Atlas.Core.Models;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

[ApiController]
[Route("api/v1/open-api-webhooks")]
[Authorize]
public sealed class OpenApiWebhooksController : ControllerBase
{
    private readonly IWebhookService _webhookService;

    public OpenApiWebhooksController(IWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenView)]
    public async Task<ActionResult<ApiResponse<object>>> GetAll(CancellationToken cancellationToken = default)
    {
        var subs = await _webhookService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(subs, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenCreate)]
    public async Task<ActionResult<ApiResponse<object>>> Create(
        [FromBody] CreateWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        var id = await _webhookService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        long id,
        [FromBody] UpdateWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        await _webhookService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        long id,
        CancellationToken cancellationToken = default)
    {
        await _webhookService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}/deliveries")]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenView)]
    public async Task<ActionResult<ApiResponse<object>>> GetDeliveries(
        long id,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var logs = await _webhookService.GetDeliveriesAsync(id, pageSize, cancellationToken);
        return Ok(ApiResponse<object>.Ok(logs, HttpContext.TraceIdentifier));
    }

    [HttpPost("{id:long}/test")]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Test(
        long id,
        CancellationToken cancellationToken = default)
    {
        await _webhookService.TestDeliveryAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { id = id.ToString() }, HttpContext.TraceIdentifier));
    }
}
