using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

namespace Atlas.Infrastructure.Services.AiPlatform.Channels.Feishu;

/// <summary>
/// 治理 M-G02-C7：飞书发布渠道适配器。
///
/// PublishAsync：
/// - 加载 <see cref="FeishuChannelCredential"/>（需先通过 credential 配置 API 写入），未配置则记 failed；
/// - 解密 AppSecret，调用 <see cref="IFeishuApiClient.GetTenantAccessTokenAsync"/> 验证一次凭据可用；
/// - 把 webhook URL（公开端点）和 AppId 摘要写入 <see cref="ChannelPublishResult.PublicMetadataJson"/>。
///
/// HandleInboundAsync：
/// - 解析飞书 webhook 事件；challenge 校验直接回包（type=url_verification）；
/// - 普通事件按 verification_token 校验真实性；msg_id 防重放（内存去重 5 分钟窗口）；
/// - im.message.receive_v1 → 取 receiver / sender / text → 调用 <see cref="IAgentChatService"/> → 调用 <see cref="IFeishuApiClient.SendImMessageAsync"/> 异步回包。
///
/// SendOutboundAsync：直接调用 <see cref="IFeishuApiClient.SendImMessageAsync"/> 推消息（卡片回包等场景）。
/// </summary>
public sealed class FeishuChannelConnector : IWorkspaceChannelConnector
{
    private const string Type = "feishu";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    // 飞书 message_id 去重窗口（5 分钟）；进程内 LRU。
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> ProcessedMessageIds = new();

    private readonly WorkspacePublishChannelRepository _channelRepository;
    private readonly WorkspaceChannelReleaseRepository _releaseRepository;
    private readonly FeishuChannelCredentialRepository _credentialRepository;
    private readonly IFeishuApiClient _feishuApi;
    private readonly LowCodeCredentialProtector _credentialProtector;
    private readonly IAgentChatService _agentChatService;
    private readonly ILogger<FeishuChannelConnector> _logger;

    public FeishuChannelConnector(
        WorkspacePublishChannelRepository channelRepository,
        WorkspaceChannelReleaseRepository releaseRepository,
        FeishuChannelCredentialRepository credentialRepository,
        IFeishuApiClient feishuApi,
        LowCodeCredentialProtector credentialProtector,
        IAgentChatService agentChatService,
        ILogger<FeishuChannelConnector> logger)
    {
        _channelRepository = channelRepository;
        _releaseRepository = releaseRepository;
        _credentialRepository = credentialRepository;
        _feishuApi = feishuApi;
        _credentialProtector = credentialProtector;
        _agentChatService = agentChatService;
        _logger = logger;
    }

    public string ChannelType => Type;

    public async Task<ChannelPublishResult> PublishAsync(ChannelPublishContext context, CancellationToken cancellationToken)
    {
        if (context.AgentId is null or <= 0)
        {
            return new ChannelPublishResult(false, "failed", null, "FeishuRequiresAgentId");
        }

        var channel = await _channelRepository.FindByIdAsync(context.TenantId, context.ChannelId, cancellationToken);
        if (channel is null || !channel.ChannelType.Equals(Type, StringComparison.OrdinalIgnoreCase))
        {
            return new ChannelPublishResult(false, "failed", null, "FeishuChannelNotFound");
        }

        var credential = await _credentialRepository.FindByChannelAsync(context.TenantId, context.ChannelId, cancellationToken);
        if (credential is null)
        {
            return new ChannelPublishResult(false, "failed", null, "FeishuCredentialMissing");
        }

        // 验证一次 token 可用 — 失败说明 AppId/AppSecret 不正确，发布失败可被审计明确归因。
        try
        {
            var appSecret = _credentialProtector.Decrypt(credential.AppSecretEnc);
            await _feishuApi.GetTenantAccessTokenAsync(context.ChannelId, credential.AppId, appSecret, cancellationToken);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "Feishu publish token check failed (channel={ChannelId})", context.ChannelId);
            return new ChannelPublishResult(false, "failed", null, ex.Message);
        }

        // 更新 agent 绑定到 credential（按本次 publish agentId 简单覆盖）
        credential.Update(
            appId: credential.AppId,
            appSecretEnc: credential.AppSecretEnc,
            verificationToken: credential.VerificationToken,
            encryptKeyEnc: credential.EncryptKeyEnc,
            agentBindingsJson: JsonSerializer.Serialize(new[]
            {
                new { agentId = context.AgentId.Value }
            }, JsonOptions));
        await _credentialRepository.UpdateAsync(credential, cancellationToken);

        var webhookUrl = $"/api/v1/runtime/channels/feishu/{channel.Id}/webhook";
        var publicMeta = new
        {
            webhookUrl,
            appId = credential.AppId,
            appIdMasked = LowCodeCredentialProtector.Mask(credential.AppId),
            instructions = "请在飞书开放平台「事件订阅」中将 Request URL 设置为本仓库部署后可访问的 webhookUrl，"
                         + "并在「权限管理」中开启 im:message + im:message:send_as_bot。",
            agentId = context.AgentId.Value.ToString()
        };
        return new ChannelPublishResult(true, "active", JsonSerializer.Serialize(publicMeta, JsonOptions), null);
    }

    public async Task<ChannelDispatchResult> HandleInboundAsync(ChannelInboundContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context.PayloadJson))
        {
            return new ChannelDispatchResult(false, null, "FeishuEmptyPayload");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(context.PayloadJson);
        }
        catch (JsonException)
        {
            return new ChannelDispatchResult(false, null, "FeishuPayloadInvalid");
        }

        using (doc)
        {
            var root = doc.RootElement;

            // 1. URL 校验事件：直接回 challenge
            if (root.TryGetProperty("type", out var typeProp) &&
                string.Equals(typeProp.GetString(), "url_verification", StringComparison.OrdinalIgnoreCase))
            {
                var challenge = root.TryGetProperty("challenge", out var c) ? c.GetString() : null;
                if (string.IsNullOrEmpty(challenge))
                {
                    return new ChannelDispatchResult(false, null, "FeishuMissingChallenge");
                }
                var responseJson = JsonSerializer.Serialize(new { challenge }, JsonOptions);
                return new ChannelDispatchResult(true, responseJson, null);
            }

            var credential = await _credentialRepository.FindByChannelAsync(context.TenantId, context.ChannelId, cancellationToken);
            if (credential is null)
            {
                return new ChannelDispatchResult(false, null, "FeishuCredentialMissing");
            }

            // 2. 业务事件：先校验 verification_token
            var headerToken = root.TryGetProperty("header", out var hdr) && hdr.TryGetProperty("token", out var tk)
                ? tk.GetString()
                : root.TryGetProperty("token", out var legacyTk) ? legacyTk.GetString() : null;
            if (string.IsNullOrEmpty(headerToken) || !string.Equals(headerToken, credential.VerificationToken, StringComparison.Ordinal))
            {
                return new ChannelDispatchResult(false, null, "FeishuVerificationTokenMismatch");
            }

            // 3. 事件类型限制：当前只处理 im.message.receive_v1
            var eventType = hdr.TryGetProperty("event_type", out var et) ? et.GetString() : null;
            if (!string.Equals(eventType, "im.message.receive_v1", StringComparison.Ordinal))
            {
                return new ChannelDispatchResult(true, JsonSerializer.Serialize(new { handled = false, reason = "unsupported_event" }, JsonOptions), null);
            }

            // 4. msg_id 去重
            var messageId = TryGet(root, "event", "message", "message_id");
            if (!string.IsNullOrEmpty(messageId) && IsDuplicateMessage(messageId))
            {
                return new ChannelDispatchResult(true, JsonSerializer.Serialize(new { handled = true, deduped = true }, JsonOptions), null);
            }

            // 5. 取消息内容
            var senderType = TryGet(root, "event", "sender", "sender_type");
            var senderId = TryGet(root, "event", "sender", "sender_id", "open_id");
            var content = TryGet(root, "event", "message", "content");
            if (string.IsNullOrEmpty(content))
            {
                return new ChannelDispatchResult(false, null, "FeishuEmptyMessageContent");
            }
            var textMessage = TryExtractText(content);

            // 6. 派发 Agent 对话
            var binding = TryParseFirstAgentBinding(credential.AgentBindingsJson);
            if (binding is null)
            {
                return new ChannelDispatchResult(false, null, "FeishuChannelNotPublished");
            }

            var ownerUser = await ResolveOwnerAsync(context.TenantId, context.ChannelId, cancellationToken);
            try
            {
                var response = await _agentChatService.ChatAsync(
                    context.TenantId,
                    ownerUser,
                    binding.Value,
                    new AgentChatRequest(null, textMessage, null, null),
                    cancellationToken);

                // 7. 异步回包到飞书
                if (!string.IsNullOrEmpty(senderId))
                {
                    await SendBackAsync(context.ChannelId, credential, senderId!, response.Content, cancellationToken);
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
    }

    public async Task SendOutboundAsync(ChannelOutboundContext context, CancellationToken cancellationToken)
    {
        var credential = await _credentialRepository.FindByChannelAsync(context.TenantId, context.ChannelId, cancellationToken);
        if (credential is null)
        {
            _logger.LogWarning("Feishu outbound skipped: credential missing (channel={ChannelId})", context.ChannelId);
            return;
        }
        var appSecret = _credentialProtector.Decrypt(credential.AppSecretEnc);
        var token = await _feishuApi.GetTenantAccessTokenAsync(context.ChannelId, credential.AppId, appSecret, cancellationToken);
        await _feishuApi.SendImMessageAsync(token, "open_id", context.ExternalUserId, context.MessageType, context.MessagePayloadJson, cancellationToken);
    }

    private async Task SendBackAsync(long channelId, FeishuChannelCredential credential, string receiverOpenId, string text, CancellationToken cancellationToken)
    {
        var appSecret = _credentialProtector.Decrypt(credential.AppSecretEnc);
        var token = await _feishuApi.GetTenantAccessTokenAsync(channelId, credential.AppId, appSecret, cancellationToken);
        var content = JsonSerializer.Serialize(new { text }, JsonOptions);
        await _feishuApi.SendImMessageAsync(token, "open_id", receiverOpenId, "text", content, cancellationToken);
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
            _logger.LogDebug(ex, "Failed to resolve feishu owner (channel={ChannelId})", channelId);
            return 0L;
        }
    }

    internal static long? TryParseFirstAgentBinding(string bindingsJson)
    {
        if (string.IsNullOrWhiteSpace(bindingsJson))
        {
            return null;
        }
        try
        {
            using var doc = JsonDocument.Parse(bindingsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                return null;
            }
            var first = doc.RootElement[0];
            if (!first.TryGetProperty("agentId", out var aid))
            {
                return null;
            }
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

    internal static string? TryGet(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var key in path)
        {
            if (!current.TryGetProperty(key, out var next))
            {
                return null;
            }
            current = next;
        }
        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            _ => current.GetRawText()
        };
    }

    internal static string TryExtractText(string contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return string.Empty;
        }
        try
        {
            // 飞书 content 是字符串化的 JSON：{"text":"..."}
            using var doc = JsonDocument.Parse(contentJson);
            if (doc.RootElement.TryGetProperty("text", out var text))
            {
                return text.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // fallthrough
        }
        return contentJson;
    }

    internal static bool IsDuplicateMessage(string messageId)
    {
        var now = DateTime.UtcNow;
        // 清理过期项
        if (ProcessedMessageIds.Count > 1024)
        {
            foreach (var pair in ProcessedMessageIds.Where(kv => kv.Value < now.AddMinutes(-5)).ToArray())
            {
                ProcessedMessageIds.TryRemove(pair.Key, out _);
            }
        }
        if (!ProcessedMessageIds.TryAdd(messageId, now))
        {
            return true;
        }
        return false;
    }
}
