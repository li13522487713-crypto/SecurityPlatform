using Atlas.Application.Metering;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Metering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// 计量与配额管理 API
/// </summary>
[ApiController]
[Route("api/v1/metering")]
[Authorize]
[PlatformOnly]
public sealed class MeteringController : ControllerBase
{
    private readonly IMeteringService _service;
    private readonly ITenantProvider _tenantProvider;

    public MeteringController(IMeteringService service, ITenantProvider tenantProvider)
    {
        _service = service;
        _tenantProvider = tenantProvider;
    }

    /// <summary>获取当前租户所有配额配置</summary>
    [HttpGet("quotas")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TenantQuota>>>> GetQuotas(CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var result = await _service.GetQuotasAsync(tenantId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<TenantQuota>>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>设置/更新配额</summary>
    [HttpPut("quotas/{resourceType}")]
    public async Task<ActionResult<ApiResponse<object>>> SetQuota(
        string resourceType,
        [FromBody] SetQuotaRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        await _service.SetQuotaAsync(tenantId, resourceType, request.MaxQuantity, request.IsEnabled, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>查询指定时间段内的累计用量</summary>
    [HttpGet("usage/{resourceType}")]
    public async Task<ActionResult<ApiResponse<object>>> GetUsage(
        string resourceType,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var total = await _service.GetTotalUsageAsync(tenantId, resourceType, from, to, cancellationToken);
        var isExceeded = await _service.IsQuotaExceededAsync(tenantId, resourceType, 0, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { ResourceType = resourceType, Total = total, IsQuotaExceeded = isExceeded }, HttpContext.TraceIdentifier));
    }
}

public sealed record SetQuotaRequest(decimal MaxQuantity, bool IsEnabled = true);
