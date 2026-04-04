using Atlas.Core.Messaging;
using Atlas.Core.Models;
using Atlas.Domain.Messaging;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using Atlas.Presentation.Shared.Filters;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// 消息队列监控与管理 API
/// </summary>
[ApiController]
[Route("api/v1/admin/message-queue")]
[Authorize(Policy = PermissionPolicies.SystemAdmin)]
public sealed class MessageQueueController : ControllerBase
{
    private readonly IMessageQueue _queue;
    private readonly ISqlSugarClient _db;

    public MessageQueueController(IMessageQueue queue, ISqlSugarClient db)
    {
        _queue = queue;
        _db = db;
    }

    /// <summary>获取所有队列统计信息</summary>
    [HttpGet("queues")]
    public async Task<ActionResult<ApiResponse<object>>> GetQueues(CancellationToken cancellationToken = default)
    {
        var allMessages = await _db.Queryable<QueueMessage>()
            .Select(m => new { m.QueueName, m.Status })
            .ToListAsync(cancellationToken);

        var queueNames = allMessages.Select(m => m.QueueName).Distinct();
        var stats = queueNames.Select(name => new
        {
            QueueName = name,
            Pending = allMessages.Count(m => m.QueueName == name && m.Status == QueueMessageStatus.Pending),
            Processing = allMessages.Count(m => m.QueueName == name && m.Status == QueueMessageStatus.Processing),
            Completed = allMessages.Count(m => m.QueueName == name && m.Status == QueueMessageStatus.Completed),
            Failed = allMessages.Count(m => m.QueueName == name && m.Status == QueueMessageStatus.Failed),
            DeadLettered = allMessages.Count(m => m.QueueName == name && m.Status == QueueMessageStatus.DeadLettered)
        }).ToList();

        return Ok(ApiResponse<object>.Ok(stats, HttpContext.TraceIdentifier));
    }

    /// <summary>按队列查看消息（分页）</summary>
    [HttpGet("queues/{name}/messages")]
    public async Task<ActionResult<ApiResponse<object>>> GetMessages(
        string name,
        [FromQuery] PagedRequest request,
        [FromQuery] QueueMessageStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Queryable<QueueMessage>().Where(m => m.QueueName == name);
        if (status.HasValue)
        {
            query = query.Where(m => m.Status == status.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(m => m.EnqueuedAt)
            .ToPageListAsync(request.PageIndex, request.PageSize, cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { request.PageIndex, request.PageSize, Total = total, Items = items }, HttpContext.TraceIdentifier));
    }

    /// <summary>批量重试死信消息</summary>
    [HttpPost("queues/{name}/dead-letters/retry")]
    public async Task<ActionResult<ApiResponse<object>>> RetryDeadLetters(
        string name,
        CancellationToken cancellationToken = default)
    {
        var count = await _db.Updateable<QueueMessage>()
            .SetColumns(m => new QueueMessage
            {
                Status = QueueMessageStatus.Pending,
                RetryCount = 0,
                NextRetryAt = null,
                ErrorMessage = null
            })
            .Where(m => m.QueueName == name && m.Status == QueueMessageStatus.DeadLettered)
            .ExecuteCommandAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { Retried = count }, HttpContext.TraceIdentifier));
    }

    /// <summary>清理死信消息</summary>
    [HttpDelete("queues/{name}/dead-letters")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDeadLetters(
        string name,
        CancellationToken cancellationToken = default)
    {
        var count = await _db.Deleteable<QueueMessage>()
            .Where(m => m.QueueName == name && m.Status == QueueMessageStatus.DeadLettered)
            .ExecuteCommandAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(new { Deleted = count }, HttpContext.TraceIdentifier));
    }

    /// <summary>全局统计</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<object>>> GetStats(CancellationToken cancellationToken = default)
    {
        var stats = await _queue.GetStatsAsync(null, cancellationToken);
        return Ok(ApiResponse<object>.Ok(stats, HttpContext.TraceIdentifier));
    }
}
