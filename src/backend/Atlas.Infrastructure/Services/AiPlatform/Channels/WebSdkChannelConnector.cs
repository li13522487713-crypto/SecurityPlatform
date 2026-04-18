using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Repositories.AiPlatform;
using Atlas.Infrastructure.Services.AiPlatform.Channels.Signatures;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform.Channels;

/// <summary>
/// 治理 M-G02-C3：Web SDK 发布渠道适配器。
///
/// 行为：
/// - PublishAsync：每次发布旋转 HMAC secret，写到 <see cref="WorkspacePublishChannel.SecretJson"/>（加密）；
///   返回 <see cref="ChannelPublishResult"/>，<c>PublicMetadataJson</c> 含 snippet、endpoint、原始 secret（仅本次返回，
///   前端需引导管理员一次性记录）、originAllowlist。
/// - HandleInboundAsync：从 channel 当前 secret 验签，对照 originAllowlist；通过后调用 <see cref="IAgentChatService"/>
///   完成 agent 对话；nonce 防重放由调用方（PublicChannelEndpointsController）维护。
/// - SendOutboundAsync：Web SDK 浏览器主动 pull，无需主动 push；本方法记录调试日志后即返回（不抛异常）。
/// </summary>
public sealed class WebSdkChannelConnector : IWorkspaceChannelConnector
{
    private const string Type = "web-sdk";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WorkspacePublishChannelRepository _channelRepository;
    private readonly WorkspaceChannelReleaseRepository _releaseRepository;
    private readonly LowCodeCredentialProtector _credentialProtector;
    private readonly IAgentChatService _agentChatService;
    private readonly ILogger<WebSdkChannelConnector> _logger;

    public WebSdkChannelConnector(
        WorkspacePublishChannelRepository channelRepository,
        WorkspaceChannelReleaseRepository releaseRepository,
        LowCodeCredentialProtector credentialProtector,
        IAgentChatService agentChatService,
        ILogger<WebSdkChannelConnector> logger)
    {
        _channelRepository = channelRepository;
        _releaseRepository = releaseRepository;
        _credentialProtector = credentialProtector;
        _agentChatService = agentChatService;
        _logger = logger;
    }

    public string ChannelType => Type;

    public async Task<ChannelPublishResult> PublishAsync(ChannelPublishContext context, CancellationToken cancellationToken)
    {
        var channel = await LoadChannelOrThrowAsync(context, cancellationToken);
        if (context.AgentId is null or <= 0)
        {
            return new ChannelPublishResult(false, "failed", null, "WebSdkRequiresAgentId");
        }

        var snapshot = TryParseSnapshot(context.ConfigSnapshotJson);
        var secret = HmacChannelSigner.GenerateSecret(32);
        var newSettings = new WebSdkChannelSecret(
            SecretKey: secret,
            AgentId: context.AgentId.Value,
            OriginAllowlist: snapshot.OriginAllowlist ?? Array.Empty<string>(),
            EmbedTokenLifetimeSeconds: snapshot.EmbedTokenLifetimeSeconds ?? 3600);

        var json = JsonSerializer.Serialize(newSettings, JsonOptions);
        channel.SetSecretJson(_credentialProtector.Encrypt(json));
        await _channelRepository.UpdateAsync(channel, cancellationToken);

        var endpoint = $"/api/v1/runtime/channels/web-sdk/{channel.Id}/messages";
        var snippet = BuildSnippet(channel.Id, endpoint, newSettings.OriginAllowlist);
        var publicMeta = new
        {
            endpoint,
            snippet,
            originAllowlist = newSettings.OriginAllowlist,
            secret,
            secretMasked = LowCodeCredentialProtector.Mask(secret),
            embedTokenLifetimeSeconds = newSettings.EmbedTokenLifetimeSeconds
        };
        return new ChannelPublishResult(true, "active", JsonSerializer.Serialize(publicMeta, JsonOptions), null);
    }

    public async Task<ChannelDispatchResult> HandleInboundAsync(ChannelInboundContext context, CancellationToken cancellationToken)
    {
        var channel = await LoadChannelByIdAsync(context.TenantId, context.ChannelId, cancellationToken);
        if (channel is null)
        {
            return new ChannelDispatchResult(false, null, "ChannelNotFound");
        }
        var secret = TryDecryptSecret(channel.SecretJson);
        if (secret is null)
        {
            return new ChannelDispatchResult(false, null, "WebSdkChannelNotPublished");
        }

        if (!context.Headers.TryGetValue("x-channel-signature", out var signature) ||
            !context.Headers.TryGetValue("x-channel-timestamp", out var timestampRaw) ||
            !context.Headers.TryGetValue("x-channel-nonce", out var nonce))
        {
            return new ChannelDispatchResult(false, null, "WebSdkSignatureHeadersMissing");
        }
        if (!long.TryParse(timestampRaw, out var timestamp))
        {
            return new ChannelDispatchResult(false, null, "WebSdkTimestampInvalid");
        }
        if (!HmacChannelSigner.Verify(secret.SecretKey, timestamp, nonce, context.PayloadJson, signature))
        {
            return new ChannelDispatchResult(false, null, "WebSdkSignatureMismatch");
        }
        if (!IsOriginAllowed(context.Headers, secret.OriginAllowlist))
        {
            return new ChannelDispatchResult(false, null, "WebSdkOriginRejected");
        }

        var payload = TryParsePayload(context.PayloadJson);
        if (string.IsNullOrWhiteSpace(payload.Message))
        {
            return new ChannelDispatchResult(false, null, "WebSdkMessageRequired");
        }

        // 真实分发到 Agent 对话；conversation 由 message-id+external-user 推导，conversationId 透传。
        var owner = await LoadActiveReleaseOwnerOrFallbackAsync(context.TenantId, channel, secret.AgentId, cancellationToken);
        var chatRequest = new AgentChatRequest(
            ConversationId: payload.ConversationId,
            Message: payload.Message,
            EnableRag: payload.EnableRag,
            Attachments: null);

        try
        {
            var response = await _agentChatService.ChatAsync(context.TenantId, owner, secret.AgentId, chatRequest, cancellationToken);
            var responseJson = JsonSerializer.Serialize(new
            {
                conversationId = response.ConversationId.ToString(),
                messageId = response.MessageId.ToString(),
                content = response.Content,
                sources = response.Sources
            }, JsonOptions);
            return new ChannelDispatchResult(true, responseJson, null);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "WebSdk dispatch business error (channel={ChannelId})", channel.Id);
            return new ChannelDispatchResult(false, null, ex.Message);
        }
    }

    public Task SendOutboundAsync(ChannelOutboundContext context, CancellationToken cancellationToken)
    {
        // Web SDK 浏览器侧 pull-only；服务端无主动推送通道。
        // 出站只记录调试日志，便于问题回溯。
        _logger.LogDebug(
            "WebSdk outbound is a no-op (channel={ChannelId} type={MessageType})",
            context.ChannelId,
            context.MessageType);
        return Task.CompletedTask;
    }

    internal static string BuildSnippet(long channelId, string endpoint, IReadOnlyList<string> origins)
    {
        var allowedOrigins = string.Join(", ", origins.Select(o => $"\"{o.Replace("\"", "\\\"")}\""));
        return string.Concat(
            "<script type=\"text/javascript\" data-atlas-web-sdk-channel=\"", channelId.ToString(), "\">",
            "(function(){",
            "var endpoint='", endpoint, "';",
            "var allowedOrigins=[", allowedOrigins, "];",
            "window.AtlasWebSdk = window.AtlasWebSdk || { channelId: '", channelId.ToString(), "', endpoint: endpoint, allowedOrigins: allowedOrigins };",
            "})();",
            "</script>");
    }

    private async Task<WorkspacePublishChannel> LoadChannelOrThrowAsync(ChannelPublishContext context, CancellationToken cancellationToken)
    {
        // ChannelPublishContext.WorkspaceId 为 long（M-G02-C1 抽象设计），但 channel 表用 string；
        // 当前实现里 channel 是先按 long Id + tenant 反查取 WorkspaceId 字符串，避免重复 Repository 调用。
        var channel = await _channelRepository.FindByIdAsync(context.TenantId, context.ChannelId, cancellationToken);
        if (channel is null || channel.ChannelType.Equals(ChannelType, StringComparison.OrdinalIgnoreCase) is false)
        {
            throw new BusinessException("CHANNEL_NOT_FOUND", "WebSdkChannelNotFound");
        }
        return channel;
    }

    private Task<WorkspacePublishChannel?> LoadChannelByIdAsync(TenantId tenantId, long channelId, CancellationToken cancellationToken)
    {
        return _channelRepository.FindByIdAsync(tenantId, channelId, cancellationToken);
    }

    private async Task<long> LoadActiveReleaseOwnerOrFallbackAsync(
        TenantId tenantId,
        WorkspacePublishChannel channel,
        long agentId,
        CancellationToken cancellationToken)
    {
        // 取 channel 当前活动 release 的发布者作为 chat 责任人；找不到时 fallback 到 0（系统 dispatch）。
        try
        {
            var release = await _releaseRepository.FindActiveAsync(tenantId, channel.WorkspaceId, channel.Id, cancellationToken);
            return release?.ReleasedByUserId ?? 0L;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve active release owner; falling back to 0 (channel={ChannelId})", channel.Id);
            return 0L;
        }
    }

    private WebSdkChannelSecret? TryDecryptSecret(string? secretJson)
    {
        if (string.IsNullOrEmpty(secretJson))
        {
            return null;
        }
        try
        {
            var decoded = _credentialProtector.Decrypt(secretJson);
            return JsonSerializer.Deserialize<WebSdkChannelSecret>(decoded, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt web-sdk channel secret");
            return null;
        }
    }

    private static bool IsOriginAllowed(IReadOnlyDictionary<string, string> headers, IReadOnlyList<string> allowlist)
    {
        if (allowlist.Count == 0)
        {
            return true;
        }
        if (allowlist.Any(o => o == "*"))
        {
            return true;
        }
        if (!headers.TryGetValue("origin", out var origin))
        {
            return false;
        }
        return allowlist.Any(allowed => string.Equals(allowed, origin, StringComparison.OrdinalIgnoreCase));
    }

    internal static WebSdkPublishSettings TryParseSnapshot(string? snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return new WebSdkPublishSettings(null, null);
        }
        try
        {
            using var doc = JsonDocument.Parse(snapshotJson);
            var root = doc.RootElement;
            var origins = root.TryGetProperty("originAllowlist", out var allow) && allow.ValueKind == JsonValueKind.Array
                ? allow.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToArray()
                : null;
            int? lifetime = root.TryGetProperty("embedTokenLifetimeSeconds", out var ttl) && ttl.ValueKind == JsonValueKind.Number
                ? ttl.GetInt32()
                : null;
            return new WebSdkPublishSettings(origins, lifetime);
        }
        catch (JsonException)
        {
            return new WebSdkPublishSettings(null, null);
        }
    }

    internal static WebSdkInboundPayload TryParsePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new WebSdkInboundPayload(null, null, null);
        }
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            long? conversationId = root.TryGetProperty("conversationId", out var cid)
                ? cid.ValueKind switch
                {
                    JsonValueKind.Number => cid.GetInt64(),
                    JsonValueKind.String when long.TryParse(cid.GetString(), out var parsed) => parsed,
                    _ => (long?)null
                }
                : null;
            bool? enableRag = root.TryGetProperty("enableRag", out var rag) && rag.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? rag.GetBoolean()
                : null;
            return new WebSdkInboundPayload(message, conversationId, enableRag);
        }
        catch (JsonException)
        {
            return new WebSdkInboundPayload(null, null, null);
        }
    }

    internal sealed record WebSdkPublishSettings(string[]? OriginAllowlist, int? EmbedTokenLifetimeSeconds);

    internal sealed record WebSdkInboundPayload(string? Message, long? ConversationId, bool? EnableRag);

    internal sealed record WebSdkChannelSecret(string SecretKey, long AgentId, IReadOnlyList<string> OriginAllowlist, int EmbedTokenLifetimeSeconds);
}
