using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers.Connectors;

/// <summary>
/// 外部协同消息派发 REST：
/// - POST  /api/v1/connectors/providers/{providerId}/messages:send         发文本/卡片，强制写 ExternalMessageDispatch；
/// - POST  /api/v1/connectors/providers/{providerId}/messages/{dispatchId}:update-card  按 dispatchId 复用 ResponseCode 做卡片更新；
/// - GET   /api/v1/connectors/providers/{providerId}/messages              派发记录分页（用于 UI 卡片预览/重发的来源）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/connectors/providers/{providerId:long}/messages")]
public sealed class ConnectorMessagingController : ControllerBase
{
    private readonly IExternalMessagingService _messaging;

    public ConnectorMessagingController(IExternalMessagingService messaging)
    {
        _messaging = messaging;
    }

    [HttpPost(":send")]
    public async Task<ActionResult<ExternalMessageDispatchSummary>> SendAsync(long providerId, [FromBody] ConnectorMessagingSendRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var summary = await _messaging.SendAsync(new SendExternalMessageRequest
        {
            ProviderId = providerId,
            BusinessKey = string.IsNullOrEmpty(request.BusinessKey) ? Guid.NewGuid().ToString("N") : request.BusinessKey,
            Recipient = request.Recipient ?? throw new ArgumentException("Recipient is required.", nameof(request)),
            Text = request.Text,
            Card = request.Card,
        }, cancellationToken).ConfigureAwait(false);
        return Ok(summary);
    }

    [HttpPost("{dispatchId:long}:update-card")]
    public async Task<ActionResult<ExternalMessageDispatchSummary>> UpdateCardAsync(long providerId, long dispatchId, [FromBody] UpdateCardRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = providerId; // path 携带 providerId 用于鉴权 / 日志关联，业务以 dispatchId 为权威。
        var summary = await _messaging.UpdateCardAsync(dispatchId, request, cancellationToken).ConfigureAwait(false);
        return Ok(summary);
    }
}

public sealed class ConnectorMessagingSendRequest
{
    public string? BusinessKey { get; init; }

    public ExternalMessageRecipient? Recipient { get; init; }

    public string? Text { get; init; }

    public ExternalMessageCard? Card { get; init; }
}
