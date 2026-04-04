using Atlas.Application.Subscription;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 套餐管理 API（平台管理员使用）
/// </summary>
[ApiController]
[Route("api/v1/plans")]
[Authorize]
[PlatformOnly]
public sealed class PlansController : ControllerBase
{
    private readonly IPlanQueryService _queryService;
    private readonly IPlanCommandService _commandService;

    public PlansController(IPlanQueryService queryService, IPlanCommandService commandService)
    {
        _queryService = queryService;
        _commandService = commandService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _queryService.GetActivePlansAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(result, HttpContext.TraceIdentifier));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> GetById(long id, CancellationToken cancellationToken)
    {
        var plan = await _queryService.GetByIdAsync(id, cancellationToken);
        if (plan is null) return NotFound(ApiResponse<object>.Fail(ErrorCodes.NotFound, ApiResponseLocalizer.T(HttpContext, "SubscriptionPlanNotFound"), HttpContext.TraceIdentifier));
        return Ok(ApiResponse<object>.Ok(plan, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] CreatePlanRequest request, CancellationToken cancellationToken)
    {
        var id = await _commandService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, HttpContext.TraceIdentifier));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(long id, [FromBody] UpdatePlanRequest request, CancellationToken cancellationToken)
    {
        await _commandService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Deactivate(long id, CancellationToken cancellationToken)
    {
        await _commandService.DeactivateAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}

/// <summary>
/// 租户订阅管理 API
/// </summary>
[ApiController]
[Route("api/v1/subscriptions")]
[Authorize]
[PlatformOnly]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _service;
    private readonly ITenantProvider _tenantProvider;

    public SubscriptionsController(ISubscriptionService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<object>>> GetCurrent(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var sub = await _service.GetCurrentAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(sub, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Subscribe([FromBody] SubscribeRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var id = await _service.SubscribeAsync(tenantId, request.PlanId, request.ExpiresAt, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Id = id }, HttpContext.TraceIdentifier));
    }

    [HttpDelete("current")]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.CancelAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    [HttpPost("current/renew")]
    public async Task<ActionResult<ApiResponse<object>>> Renew([FromBody] RenewSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.RenewAsync(tenantId, request.NewExpiresAt, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}

public sealed record SubscribeRequest(long PlanId, DateTimeOffset? ExpiresAt);
public sealed record RenewSubscriptionRequest(DateTimeOffset NewExpiresAt);
