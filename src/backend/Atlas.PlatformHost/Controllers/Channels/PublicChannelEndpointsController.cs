using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Atlas.PlatformHost.Controllers.Channels;

/// <summary>
/// 治理 M-G02-C3 / C4：渠道公共入口（Web SDK / Open API）。
///
/// 不挂 JWT 鉴权，鉴权由具体 connector 在 HandleInboundAsync 内部完成
/// （Web SDK = HMAC + Origin 白名单；Open API = Bearer + 限流）。
/// 路由规则：<c>/api/v1/runtime/channels/{channelType}/{channelId}/messages</c>。
/// 注：PlatformHost 的 <c>ApiVersionRewriteMiddleware</c> 会把 <c>/api/runtime/...</c> 重写到 <c>/api/v1/runtime/...</c>，
/// 因此外部调用 <c>/api/runtime/...</c> 与 <c>/api/v1/runtime/...</c> 等价。
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/runtime/channels")]
public sealed class PublicChannelEndpointsController : ControllerBase
{
    private readonly IWorkspaceChannelConnectorRegistry _registry;
    private readonly ILogger<PublicChannelEndpointsController> _logger;

    public PublicChannelEndpointsController(
        IWorkspaceChannelConnectorRegistry registry,
        ILogger<PublicChannelEndpointsController> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    [HttpPost("{channelType}/{channelId:long}/messages")]
    public Task<IActionResult> InboundMessages(string channelType, long channelId, CancellationToken cancellationToken)
        => DispatchAsync(channelType, channelId, cancellationToken);

    [HttpPost("{channelType}/{channelId:long}/chat")]
    public Task<IActionResult> InboundChat(string channelType, long channelId, CancellationToken cancellationToken)
        => DispatchAsync(channelType, channelId, cancellationToken);

    private async Task<IActionResult> DispatchAsync(
        string channelType,
        long channelId,
        CancellationToken cancellationToken)
    {
        var connector = _registry.Resolve(channelType);
        if (connector is null)
        {
            return NotFound(new { code = "CHANNEL_TYPE_NOT_SUPPORTED", channelType });
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
            ChannelType: channelType,
            EventType: "message",
            ExternalUserId: headers.TryGetValue("x-channel-external-user", out var ext) ? ext : null,
            Conversation: headers.TryGetValue("x-channel-conversation", out var conv) ? conv : null,
            PayloadJson: body,
            Headers: headers);

        var result = await connector.HandleInboundAsync(ctx, cancellationToken);
        if (!result.Handled)
        {
            _logger.LogWarning(
                "Channel inbound rejected (channelType={ChannelType}, channelId={ChannelId}, reason={Reason})",
                channelType, channelId, result.FailureReason);
            return Unauthorized(new ApiResponse<object>(
                Success: false,
                Code: result.FailureReason ?? "CHANNEL_DISPATCH_FAILED",
                Message: result.FailureReason ?? "channel dispatch failed",
                TraceId: HttpContext.TraceIdentifier,
                Data: null));
        }

        if (string.IsNullOrEmpty(result.AgentResponseJson))
        {
            return Ok(ApiResponse<object>.Ok(new { handled = true }, HttpContext.TraceIdentifier));
        }

        try
        {
            using var doc = JsonDocument.Parse(result.AgentResponseJson);
            return Ok(ApiResponse<JsonElement>.Ok(doc.RootElement.Clone(), HttpContext.TraceIdentifier));
        }
        catch (JsonException)
        {
            return Ok(ApiResponse<object>.Ok(new { handled = true, response = result.AgentResponseJson }, HttpContext.TraceIdentifier));
        }
    }
}
