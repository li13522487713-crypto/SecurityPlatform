using Atlas.Application.Events;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 平台统一事件历史查询 API
/// </summary>
[ApiController]
[Route("api/v1/platform-events")]
public sealed class PlatformEventsController : ControllerBase
{
    private readonly IPlatformEventService _service;
    private readonly ITenantProvider _tenantProvider;

    public PlatformEventsController(IPlatformEventService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PlatformEventsView)]
    public async Task<ActionResult<ApiResponse<object>>> Query(
        [FromQuery] string? eventType,
        [FromQuery] bool? isProcessed,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.QueryAsync(tenantId, eventType, isProcessed, from, to, pageIndex, pageSize, cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, HttpContext.TraceIdentifier));
    }
}
