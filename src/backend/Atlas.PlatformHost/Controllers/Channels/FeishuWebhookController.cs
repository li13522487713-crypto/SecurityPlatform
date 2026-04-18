using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Atlas.PlatformHost.Controllers.Channels;

/// <summary>
/// 治理 M-G02-C7：飞书 webhook 入口（事件订阅）。
///
/// - 路由：<c>/api/v1/runtime/channels/feishu/{channelId}/webhook</c>
/// - 请求头要求 <c>X-Tenant-Id</c>（部署侧由飞书回调时按租户路由网关或反代填充）。
/// - 飞书首次校验 URL 时回 challenge：connector 内部 short-circuit 直接返 challenge JSON。
/// - 业务事件：返回 200 + 简单 JSON（飞书要求 5 秒内 ack；真实回包通过 connector 内部 SendBackAsync 异步走 IM API）。
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/runtime/channels/feishu/{channelId:long}")]
public sealed class FeishuWebhookController : ControllerBase
{
    private readonly IWorkspaceChannelConnectorRegistry _registry;
    private readonly ILogger<FeishuWebhookController> _logger;

    public FeishuWebhookController(
        IWorkspaceChannelConnectorRegistry registry,
        ILogger<FeishuWebhookController> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(long channelId, CancellationToken cancellationToken)
    {
        var connector = _registry.Resolve("feishu");
        if (connector is null)
        {
            return NotFound(new { code = "CHANNEL_TYPE_NOT_SUPPORTED", channelType = "feishu" });
        }

        var tenantHeader = Request.Headers["X-Tenant-Id"].ToString();
        if (string.IsNullOrWhiteSpace(tenantHeader) || !Guid.TryParse(tenantHeader, out var tenantGuid))
        {
            return BadRequest(new { code = "TENANT_HEADER_REQUIRED" });
        }

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in Request.Headers)
        {
            headers[header.Key] = header.Value.ToString();
        }

        var ctx = new ChannelInboundContext(
            TenantId: new TenantId(tenantGuid),
            ChannelId: channelId,
            ChannelType: "feishu",
            EventType: "webhook",
            ExternalUserId: null,
            Conversation: null,
            PayloadJson: body,
            Headers: headers);

        var result = await connector.HandleInboundAsync(ctx, cancellationToken);
        if (!result.Handled)
        {
            _logger.LogWarning(
                "Feishu webhook rejected (channelId={ChannelId}, reason={Reason})",
                channelId, result.FailureReason);
            // 飞书要求 200，否则会重试无意义；以业务字段表达失败。
            return Ok(new { handled = false, reason = result.FailureReason });
        }

        if (string.IsNullOrEmpty(result.AgentResponseJson))
        {
            return Ok(new { handled = true });
        }

        try
        {
            using var doc = JsonDocument.Parse(result.AgentResponseJson);
            return Content(result.AgentResponseJson, "application/json", Encoding.UTF8);
        }
        catch (JsonException)
        {
            return Ok(new { handled = true, response = result.AgentResponseJson });
        }
    }
}
