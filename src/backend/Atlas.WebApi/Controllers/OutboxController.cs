using Atlas.Application.Events;
using Atlas.Core.Models;
using Atlas.WebApi.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Atlas.WebApi.Filters;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// Outbox 死信管理 API（等保2.0：系统管理员可管理集成事件死信）
/// </summary>
[ApiController]
[Route("api/v1/admin/outbox")]
[Authorize]
[PlatformOnly]
public sealed class OutboxController : ControllerBase
{
    private readonly IOutboxManagementService _outboxService;

    public OutboxController(IOutboxManagementService outboxService)
    {
        _outboxService = outboxService;
    }

    /// <summary>获取 Outbox 消息统计（各状态数量）</summary>
    [HttpGet("stats")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<OutboxStats>>> GetStats(
        CancellationToken cancellationToken = default)
    {
        var stats = await _outboxService.GetStatsAsync(cancellationToken);
        return Ok(ApiResponse<OutboxStats>.Ok(stats, HttpContext.TraceIdentifier));
    }

    /// <summary>获取死信消息列表（分页）</summary>
    [HttpGet("dead-letters")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> GetDeadLetters(
        [FromQuery] PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _outboxService.GetDeadLetteredAsync(request.PageIndex, request.PageSize, cancellationToken);

        var result = new
        {
            request.PageIndex,
            request.PageSize,
            Total = total,
            Items = items.Select(m => new
            {
                m.Id,
                m.EventId,
                m.EventType,
                m.TenantId,
                m.Status,
                m.CreatedAt,
                m.RetryCount,
                m.MaxRetries,
                m.ErrorMessage,
                m.NextRetryAt
            }).ToList()
        };

        return Ok(ApiResponse<object>.Ok(result, HttpContext.TraceIdentifier));
    }

    /// <summary>手动重试单条死信消息</summary>
    [HttpPost("dead-letters/{id:long}/retry")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> RetryDeadLetter(
        long id,
        CancellationToken cancellationToken = default)
    {
        await _outboxService.RetryDeadLetterAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }

    /// <summary>批量重试所有死信消息</summary>
    [HttpPost("dead-letters/retry-all")]
    [Authorize(Policy = PermissionPolicies.SystemAdmin)]
    public async Task<ActionResult<ApiResponse<object>>> RetryAllDeadLetters(
        CancellationToken cancellationToken = default)
    {
        await _outboxService.RetryAllDeadLettersAsync(cancellationToken);
        return Ok(ApiResponse<object>.Ok(null, HttpContext.TraceIdentifier));
    }
}
