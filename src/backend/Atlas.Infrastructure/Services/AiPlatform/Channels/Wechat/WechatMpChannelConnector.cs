using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Entities.Channels;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Repositories.AiPlatform;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform.Channels.Wechat;

/// <summary>
/// 治理 M-G02-C11：微信公众号渠道适配器。
///
/// PublishAsync：
/// - 校验 <see cref="WechatMpChannelCredential"/> 必须存在；
/// - 调用 <see cref="IWechatMpApiClient.GetAccessTokenAsync"/> 验证一次凭据可用；
/// - 把 webhook URL（公开端点）+ AppId 摘要写到 PublicMetadataJson。
///
/// HandleInboundAsync：
/// - 走 webhook：先校验 signature = SHA1(sort([token, timestamp, nonce]).join(''))；
/// - 解析 XML 消息体；MsgId 5 分钟内存去重；
/// - text 消息派发到 Agent 对话，回包通过 <see cref="IWechatMpApiClient.SendCustomerMessageAsync"/>（客服消息接口，5 秒外仍可发送）。
///
/// 入站上下文约定：调用方（FeishuWebhookController 同款的 WechatMpWebhookController）需要把
/// signature/timestamp/nonce/echostr 写到 Headers，以避免将查询参数耦合进 connector 接口。
/// </summary>
public sealed class WechatMpChannelConnector : IWorkspaceChannelConnector
{
    private const string Type = "wechat-mp";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> ProcessedMessageIds = new();

    private readonly WorkspacePublishChannelRepository _channelRepository;
    private readonly WorkspaceChannelReleaseRepository _releaseRepository;
    private readonly WechatMpChannelCredentialRepository _credentialRepository;
    private readonly IWechatMpApiClient _wechatApi;
    private readonly LowCodeCredentialProtector _credentialProtector;
    private readonly IAgentChatService _agentChatService;
    private readonly ILogger<WechatMpChannelConnector> _logger;

    public WechatMpChannelConnector(
        WorkspacePublishChannelRepository channelRepository,
        WorkspaceChannelReleaseRepository releaseRepository,
        WechatMpChannelCredentialRepository credentialRepository,
        IWechatMpApiClient wechatApi,
        LowCodeCredentialProtector credentialProtector,
        IAgentChatService agentChatService,
        ILogger<WechatMpChannelConnector> logger)
    {
        _channelRepository = channelRepository;
        _releaseRepository = releaseRepository;
        _credentialRepository = credentialRepository;
        _wechatApi = wechatApi;
        _credentialProtector = credentialProtector;
        _agentChatService = agentChatService;
        _logger = logger;
    }

    public string ChannelType => Type;

    public async Task<ChannelPublishResult> PublishAsync(ChannelPublishContext context, CancellationToken cancellationToken)
    {
        if (context.AgentId is null or <= 0)
        {
            return new ChannelPublishResult(false, "failed", null, "WechatMpRequiresAgentId");
        }
        var channel = await _channelRepository.FindByIdAsync(context.TenantId, context.ChannelId, cancellationToken);
        if (channel is null || !channel.ChannelType.Equals(Type, StringComparison.OrdinalIgnoreCase))
        {
            return new ChannelPublishResult(false, "failed", null, "WechatMpChannelNotFound");
        }
        var credential = await _credentialRepository.FindByChannelAsync(context.TenantId, context.ChannelId, cancellationToken);
        if (credential is null)
        {
            return new ChannelPublishResult(false, "failed", null, "WechatMpCredentialMissing");
        }

        try
        {
            var appSecret = _credentialProtector.Decrypt(credential.AppSecretEnc);
            await _wechatApi.GetAccessTokenAsync(context.ChannelId, credential.AppId, appSecret, cancellationToken);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "Wechat-mp publish token check failed (channel={ChannelId})", context.ChannelId);
            return new ChannelPublishResult(false, "failed", null, ex.Message);
        }

        credential.Update(
            appId: credential.AppId,
            appSecretEnc: credential.AppSecretEnc,
            token: credential.Token,
            encodingAesKeyEnc: credential.EncodingAesKeyEnc,
            agentBindingsJson: JsonSerializer.Serialize(new[]
            {
                new { agentId = context.AgentId.Value }
            }, JsonOptions));
        await _credentialRepository.UpdateAsync(credential, cancellationToken);

        var webhookUrl = $"/api/v1/runtime/channels/wechat-mp/{channel.Id}/webhook";
        var publicMeta = new
        {
            webhookUrl,
            appId = credential.AppId,
            appIdMasked = LowCodeCredentialProtector.Mask(credential.AppId),
            agentId = context.AgentId.Value.ToString(),
            instructions = "在微信公众平台「基本配置」中将「服务器地址 (URL)」设置为 webhookUrl 并启用消息加解密；"
                         + "在「客服功能」中开通客服后，本 connector 会通过客服消息接口异步回包。"
        };
        return new ChannelPublishResult(true, "active", JsonSerializer.Serialize(publicMeta, JsonOptions), null);
    }

    public async Task<ChannelDispatchResult> HandleInboundAsync(ChannelInboundContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context.PayloadJson) && !context.Headers.ContainsKey("echostr"))
        {
            return new ChannelDispatchResult(false, null, "WechatMpEmptyPayload");
        }

        var credential = await _credentialRepository.FindByChannelAsync(context.TenantId, context.ChannelId, cancellationToken);
        if (credential is null)
        {
            return new ChannelDispatchResult(false, null, "WechatMpCredentialMissing");
        }

        if (!context.Headers.TryGetValue("signature", out var signature) ||
            !context.Headers.TryGetValue("timestamp", out var timestamp) ||
            !context.Headers.TryGetValue("nonce", out var nonce))
        {
            return new ChannelDispatchResult(false, null, "WechatMpSignatureParamsMissing");
        }
        if (!VerifySignature(credential.Token, timestamp, nonce, signature))
        {
            return new ChannelDispatchResult(false, null, "WechatMpSignatureMismatch");
        }

        // GET 验证（echostr）
        if (context.Headers.TryGetValue("echostr", out var echo) && string.IsNullOrEmpty(context.PayloadJson))
        {
            return new ChannelDispatchResult(true, JsonSerializer.Serialize(new { echostr = echo }, JsonOptions), null);
        }

        // POST 业务事件（XML）
        var msg = TryParseXmlMessage(context.PayloadJson);
        if (msg is null)
        {
            return new ChannelDispatchResult(false, null, "WechatMpPayloadInvalid");
        }
        if (!string.IsNullOrEmpty(msg.MsgId) && IsDuplicate(msg.MsgId))
        {
            return new ChannelDispatchResult(true, JsonSerializer.Serialize(new { handled = true, deduped = true }, JsonOptions), null);
        }
        if (!string.Equals(msg.MsgType, "text", StringComparison.OrdinalIgnoreCase))
        {
            return new ChannelDispatchResult(true, JsonSerializer.Serialize(new { handled = false, reason = "unsupported_msg_type" }, JsonOptions), null);
        }

        var binding = TryFirstAgentBinding(credential.AgentBindingsJson);
        if (binding is null)
        {
            return new ChannelDispatchResult(false, null, "WechatMpChannelNotPublished");
        }

        var owner = await ResolveOwnerAsync(context.TenantId, context.ChannelId, cancellationToken);
        try
        {
            var response = await _agentChatService.ChatAsync(
                context.TenantId,
                owner,
                binding.Value,
                new AgentChatRequest(null, msg.Content, null, null),
                cancellationToken);

            // 异步回包
            try
            {
                var appSecret = _credentialProtector.Decrypt(credential.AppSecretEnc);
                var token = await _wechatApi.GetAccessTokenAsync(context.ChannelId, credential.AppId, appSecret, cancellationToken);
                await _wechatApi.SendCustomerMessageAsync(token, msg.FromUserName, "text", response.Content, cancellationToken);
            }
            catch (BusinessException ex)
            {
                _logger.LogWarning(ex, "Wechat-mp custom message send failed (channel={ChannelId})", context.ChannelId);
            }

            var resp = JsonSerializer.Serialize(new
            {
                handled = true,
                conversationId = response.ConversationId.ToString(),
                messageId = response.MessageId.ToString(),
                content = response.Content
            }, JsonOptions);
            return new ChannelDispatchResult(true, resp, null);
        }
        catch (BusinessException ex)
        {
            return new ChannelDispatchResult(false, null, ex.Message);
        }
    }

    public async Task SendOutboundAsync(ChannelOutboundContext context, CancellationToken cancellationToken)
    {
        var credential = await _credentialRepository.FindByChannelAsync(context.TenantId, context.ChannelId, cancellationToken);
        if (credential is null)
        {
            _logger.LogWarning("Wechat-mp outbound skipped: credential missing (channel={ChannelId})", context.ChannelId);
            return;
        }
        var appSecret = _credentialProtector.Decrypt(credential.AppSecretEnc);
        var token = await _wechatApi.GetAccessTokenAsync(context.ChannelId, credential.AppId, appSecret, cancellationToken);
        await _wechatApi.SendCustomerMessageAsync(token, context.ExternalUserId, context.MessageType, context.MessagePayloadJson, cancellationToken);
    }

    internal static bool VerifySignature(string token, string timestamp, string nonce, string signature)
    {
        if (string.IsNullOrEmpty(signature)) return false;
        var parts = new[] { token ?? string.Empty, timestamp ?? string.Empty, nonce ?? string.Empty };
        Array.Sort(parts, StringComparer.Ordinal);
        var raw = string.Concat(parts);
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(raw));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        var expected = sb.ToString();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signature.ToLowerInvariant()));
    }

    internal static WechatXmlMessage? TryParseXmlMessage(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return null;
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var root = doc.DocumentElement;
            if (root is null) return null;
            return new WechatXmlMessage(
                ToUserName: root.SelectSingleNode("ToUserName")?.InnerText ?? string.Empty,
                FromUserName: root.SelectSingleNode("FromUserName")?.InnerText ?? string.Empty,
                CreateTime: root.SelectSingleNode("CreateTime")?.InnerText ?? string.Empty,
                MsgType: root.SelectSingleNode("MsgType")?.InnerText ?? string.Empty,
                Content: root.SelectSingleNode("Content")?.InnerText ?? string.Empty,
                MsgId: root.SelectSingleNode("MsgId")?.InnerText ?? string.Empty);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    internal static long? TryFirstAgentBinding(string bindingsJson)
    {
        if (string.IsNullOrWhiteSpace(bindingsJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(bindingsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0) return null;
            var first = doc.RootElement[0];
            if (!first.TryGetProperty("agentId", out var aid)) return null;
            return aid.ValueKind switch
            {
                JsonValueKind.Number => aid.GetInt64(),
                JsonValueKind.String when long.TryParse(aid.GetString(), out var parsed) => parsed,
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static bool IsDuplicate(string msgId)
    {
        var now = DateTime.UtcNow;
        if (ProcessedMessageIds.Count > 1024)
        {
            foreach (var pair in ProcessedMessageIds.Where(kv => kv.Value < now.AddMinutes(-5)).ToArray())
            {
                ProcessedMessageIds.TryRemove(pair.Key, out _);
            }
        }
        return !ProcessedMessageIds.TryAdd(msgId, now);
    }

    private async Task<long> ResolveOwnerAsync(TenantId tenantId, long channelId, CancellationToken cancellationToken)
    {
        try
        {
            var channel = await _channelRepository.FindByIdAsync(tenantId, channelId, cancellationToken);
            if (channel is null) return 0L;
            var release = await _releaseRepository.FindActiveAsync(tenantId, channel.WorkspaceId, channelId, cancellationToken);
            return release?.ReleasedByUserId ?? 0L;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve wechat-mp owner (channel={ChannelId})", channelId);
            return 0L;
        }
    }

    internal sealed record WechatXmlMessage(
        string ToUserName,
        string FromUserName,
        string CreateTime,
        string MsgType,
        string Content,
        string MsgId);
}
