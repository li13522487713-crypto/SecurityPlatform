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
/// 治理 M-G02-C11：微信公众号 webhook 入口（消息推送）。
///
/// - GET 用于服务器地址校验：必带 signature/timestamp/nonce/echostr 查询参数；
/// - POST 用于业务消息：XML 体；同样需要 signature/timestamp/nonce 查询参数；
/// - 路由：<c>/api/v1/runtime/channels/wechat-mp/{channelId}/webhook</c>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/runtime/channels/wechat-mp/{channelId:long}")]
public sealed class WechatMpWebhookController : ControllerBase
{
    private readonly IWorkspaceChannelConnectorRegistry _registry;
    private readonly ILogger<WechatMpWebhookController> _logger;

    public WechatMpWebhookController(
        IWorkspaceChannelConnectorRegistry registry,
        ILogger<WechatMpWebhookController> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    [HttpGet("webhook")]
    public async Task<IActionResult> Verify(
        long channelId,
        [FromQuery] string? signature,
        [FromQuery] string? timestamp,
        [FromQuery] string? nonce,
        [FromQuery] string? echostr,
        CancellationToken cancellationToken)
    {
        var (handled, body) = await DispatchAsync(channelId, signature, timestamp, nonce, echostr, payload: string.Empty, cancellationToken);
        if (!handled)
        {
            return Unauthorized(body);
        }
        // 微信 GET 校验需要直接返回 echostr 文本（无 JSON 包裹），尝试解析 JSON 中 echostr 字段。
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("echostr", out var e))
            {
                return Content(e.GetString() ?? string.Empty, "text/plain", Encoding.UTF8);
            }
        }
        catch (JsonException)
        {
            // ignore
        }
        return Content(echostr ?? string.Empty, "text/plain", Encoding.UTF8);
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(
        long channelId,
        [FromQuery] string? signature,
        [FromQuery] string? timestamp,
        [FromQuery] string? nonce,
        CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var (handled, response) = await DispatchAsync(channelId, signature, timestamp, nonce, echostr: null, payload: body, cancellationToken);
        if (!handled)
        {
            // 微信对 POST 失败也建议 200，避免重试风暴；以业务字段表达失败。
            return Ok(new { handled = false, response });
        }
        return Content(response, "application/json", Encoding.UTF8);
    }

    private async Task<(bool Handled, string Body)> DispatchAsync(
        long channelId,
        string? signature,
        string? timestamp,
        string? nonce,
        string? echostr,
        string payload,
        CancellationToken cancellationToken)
    {
        var connector = _registry.Resolve("wechat-mp");
        if (connector is null)
        {
            return (false, "{\"code\":\"CHANNEL_TYPE_NOT_SUPPORTED\"}");
        }

        var tenantHeader = Request.Headers["X-Tenant-Id"].ToString();
        if (string.IsNullOrWhiteSpace(tenantHeader) || !Guid.TryParse(tenantHeader, out var tenantGuid))
        {
            return (false, "{\"code\":\"TENANT_HEADER_REQUIRED\"}");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(signature)) headers["signature"] = signature;
        if (!string.IsNullOrEmpty(timestamp)) headers["timestamp"] = timestamp;
        if (!string.IsNullOrEmpty(nonce)) headers["nonce"] = nonce;
        if (!string.IsNullOrEmpty(echostr)) headers["echostr"] = echostr;

        var ctx = new ChannelInboundContext(
            TenantId: new TenantId(tenantGuid),
            ChannelId: channelId,
            ChannelType: "wechat-mp",
            EventType: "webhook",
            ExternalUserId: null,
            Conversation: null,
            PayloadJson: payload,
            Headers: headers);
        var result = await connector.HandleInboundAsync(ctx, cancellationToken);
        if (!result.Handled)
        {
            _logger.LogWarning(
                "Wechat-mp webhook rejected (channelId={ChannelId}, reason={Reason})",
                channelId, result.FailureReason);
            return (false, "{\"code\":\"" + (result.FailureReason ?? "WECHAT_MP_DISPATCH_FAILED") + "\"}");
        }
        return (true, result.AgentResponseJson ?? "{\"handled\":true}");
    }
}
