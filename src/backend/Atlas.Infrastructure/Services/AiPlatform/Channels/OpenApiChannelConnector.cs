using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Exceptions;
using Atlas.Core.Resilience;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Repositories.AiPlatform;
using Atlas.Infrastructure.Resilience;
using Atlas.Infrastructure.Services.AiPlatform.Channels.Signatures;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform.Channels;

/// <summary>
/// 治理 M-G02-C4：Open API 发布渠道适配器。
///
/// - PublishAsync：旋转 tenantToken（bearer），写到 channel.SecretJson；返回 endpoint catalog + token + rateLimit。
/// - HandleInboundAsync：bearer 校验 + per-channel 限流 + 调用 Agent；返回 agent 响应。
/// - SendOutboundAsync：Open API 是请求-响应模型，无需主动外推；保留 no-op 并记日志。
///
/// 限流：每个 channel 独立窗口（默认 60 QPM = 1 QPS），实现走 InMemoryRateLimiter。生产建议替换为分布式实现。
/// </summary>
public sealed class OpenApiChannelConnector : IWorkspaceChannelConnector
{
    private const string Type = "open-api";
    private const int DefaultRatePerMinute = 60;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyList<string> DefaultEndpoints = new[] { "/chat" };

    private readonly WorkspacePublishChannelRepository _channelRepository;
    private readonly WorkspaceChannelReleaseRepository _releaseRepository;
    private readonly LowCodeCredentialProtector _credentialProtector;
    private readonly IAgentChatService _agentChatService;
    // 跨请求共享：connector 是 Scoped，rate limiter 必须 static 才能在多个请求间维持窗口。
    private static readonly Dictionary<string, IRateLimiter> RateLimiterByChannel = new();
    private static readonly object RateLimiterSync = new();
    private readonly ILogger<OpenApiChannelConnector> _logger;

    public OpenApiChannelConnector(
        WorkspacePublishChannelRepository channelRepository,
        WorkspaceChannelReleaseRepository releaseRepository,
        LowCodeCredentialProtector credentialProtector,
        IAgentChatService agentChatService,
        ILogger<OpenApiChannelConnector> logger)
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
            return new ChannelPublishResult(false, "failed", null, "OpenApiRequiresAgentId");
        }

        var settings = TryParseSnapshot(context.ConfigSnapshotJson);
        var token = HmacChannelSigner.GenerateSecret(32);
        var secret = new OpenApiChannelSecret(
            TenantToken: token,
            AgentId: context.AgentId.Value,
            RateLimitPerMinute: settings.RateLimitPerMinute ?? DefaultRatePerMinute,
            Endpoints: settings.Endpoints?.Length > 0 ? settings.Endpoints : DefaultEndpoints);

        channel.SetSecretJson(_credentialProtector.Encrypt(JsonSerializer.Serialize(secret, JsonOptions)));
        await _channelRepository.UpdateAsync(channel, cancellationToken);

        ResetRateLimiter(channel.Id, secret.RateLimitPerMinute);

        var publicMeta = new
        {
            endpoint = $"/api/v1/runtime/channels/open-api/{channel.Id}/chat",
            tenantToken = token,
            tokenMasked = LowCodeCredentialProtector.Mask(token),
            endpoints = secret.Endpoints,
            rateLimitPerMinute = secret.RateLimitPerMinute
        };
        return new ChannelPublishResult(true, "active", JsonSerializer.Serialize(publicMeta, JsonOptions), null);
    }

    public async Task<ChannelDispatchResult> HandleInboundAsync(ChannelInboundContext context, CancellationToken cancellationToken)
    {
        var channel = await _channelRepository.FindByIdAsync(context.TenantId, context.ChannelId, cancellationToken);
        if (channel is null)
        {
            return new ChannelDispatchResult(false, null, "ChannelNotFound");
        }
        var secret = TryDecryptSecret(channel.SecretJson);
        if (secret is null)
        {
            return new ChannelDispatchResult(false, null, "OpenApiChannelNotPublished");
        }

        if (!context.Headers.TryGetValue("authorization", out var authHeader) || string.IsNullOrWhiteSpace(authHeader))
        {
            return new ChannelDispatchResult(false, null, "OpenApiAuthorizationMissing");
        }
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return new ChannelDispatchResult(false, null, "OpenApiAuthorizationScheme");
        }
        var presentedToken = authHeader["Bearer ".Length..].Trim();
        if (!ConstantTimeEquals(presentedToken, secret.TenantToken))
        {
            return new ChannelDispatchResult(false, null, "OpenApiTokenMismatch");
        }

        var rateLimiter = ResolveRateLimiter(channel.Id, secret.RateLimitPerMinute);
        var allowed = await rateLimiter.TryAcquireAsync($"{context.TenantId.Value}:{channel.Id}", cancellationToken);
        if (!allowed)
        {
            return new ChannelDispatchResult(false, null, "OpenApiRateLimited");
        }

        var payload = TryParsePayload(context.PayloadJson);
        if (string.IsNullOrWhiteSpace(payload.Message))
        {
            return new ChannelDispatchResult(false, null, "OpenApiMessageRequired");
        }

        var owner = await ResolveOwnerAsync(context.TenantId, channel, cancellationToken);
        try
        {
            var response = await _agentChatService.ChatAsync(
                context.TenantId,
                owner,
                secret.AgentId,
                new AgentChatRequest(payload.ConversationId, payload.Message, payload.EnableRag, null),
                cancellationToken);
            var body = JsonSerializer.Serialize(new
            {
                conversationId = response.ConversationId.ToString(),
                messageId = response.MessageId.ToString(),
                content = response.Content,
                sources = response.Sources
            }, JsonOptions);
            return new ChannelDispatchResult(true, body, null);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "OpenApi dispatch business error (channel={ChannelId})", channel.Id);
            return new ChannelDispatchResult(false, null, ex.Message);
        }
    }

    public Task SendOutboundAsync(ChannelOutboundContext context, CancellationToken cancellationToken)
    {
        // OpenAPI 为请求/响应通道，没有主动外推；no-op + debug log 便于运维定位调用端误用。
        _logger.LogDebug("OpenApi outbound is a no-op (channel={ChannelId} type={MessageType})", context.ChannelId, context.MessageType);
        return Task.CompletedTask;
    }

    private async Task<WorkspacePublishChannel> LoadChannelOrThrowAsync(ChannelPublishContext context, CancellationToken cancellationToken)
    {
        var channel = await _channelRepository.FindByIdAsync(context.TenantId, context.ChannelId, cancellationToken);
        if (channel is null || !channel.ChannelType.Equals(ChannelType, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("CHANNEL_NOT_FOUND", "OpenApiChannelNotFound");
        }
        return channel;
    }

    private async Task<long> ResolveOwnerAsync(TenantId tenantId, WorkspacePublishChannel channel, CancellationToken cancellationToken)
    {
        try
        {
            var release = await _releaseRepository.FindActiveAsync(tenantId, channel.WorkspaceId, channel.Id, cancellationToken);
            return release?.ReleasedByUserId ?? 0L;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to resolve openapi active owner (channel={ChannelId})", channel.Id);
            return 0L;
        }
    }

    private OpenApiChannelSecret? TryDecryptSecret(string? secretJson)
    {
        if (string.IsNullOrEmpty(secretJson))
        {
            return null;
        }
        try
        {
            var decoded = _credentialProtector.Decrypt(secretJson);
            return JsonSerializer.Deserialize<OpenApiChannelSecret>(decoded, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt open-api channel secret");
            return null;
        }
    }

    private static void ResetRateLimiter(long channelId, int ratePerMinute)
    {
        var key = channelId.ToString();
        lock (RateLimiterSync)
        {
            RateLimiterByChannel[key] = new InMemoryRateLimiter(ratePerMinute < 1 ? DefaultRatePerMinute : ratePerMinute, TimeSpan.FromMinutes(1));
        }
    }

    private static IRateLimiter ResolveRateLimiter(long channelId, int ratePerMinute)
    {
        var key = channelId.ToString();
        lock (RateLimiterSync)
        {
            if (RateLimiterByChannel.TryGetValue(key, out var existing))
            {
                return existing;
            }
            var created = new InMemoryRateLimiter(ratePerMinute < 1 ? DefaultRatePerMinute : ratePerMinute, TimeSpan.FromMinutes(1));
            RateLimiterByChannel[key] = created;
            return created;
        }
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a is null || b is null)
        {
            return false;
        }
        if (a.Length != b.Length)
        {
            return false;
        }
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a),
            System.Text.Encoding.UTF8.GetBytes(b));
    }

    internal static OpenApiPublishSettings TryParseSnapshot(string? snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            return new OpenApiPublishSettings(null, null);
        }
        try
        {
            using var doc = JsonDocument.Parse(snapshotJson);
            var root = doc.RootElement;
            int? rateLimit = root.TryGetProperty("rateLimitPerMinute", out var r) && r.ValueKind == JsonValueKind.Number
                ? r.GetInt32()
                : null;
            string[]? endpoints = root.TryGetProperty("endpoints", out var e) && e.ValueKind == JsonValueKind.Array
                ? Enumerate(e)
                : null;
            return new OpenApiPublishSettings(rateLimit, endpoints);
        }
        catch (JsonException)
        {
            return new OpenApiPublishSettings(null, null);
        }
    }

    private static string[] Enumerate(JsonElement array)
    {
        var list = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    list.Add(s);
                }
            }
        }
        return list.ToArray();
    }

    internal static OpenApiInboundPayload TryParsePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new OpenApiInboundPayload(null, null, null);
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
            return new OpenApiInboundPayload(message, conversationId, enableRag);
        }
        catch (JsonException)
        {
            return new OpenApiInboundPayload(null, null, null);
        }
    }

    internal sealed record OpenApiPublishSettings(int? RateLimitPerMinute, string[]? Endpoints);

    internal sealed record OpenApiInboundPayload(string? Message, long? ConversationId, bool? EnableRag);

    internal sealed record OpenApiChannelSecret(
        string TenantToken,
        long AgentId,
        int RateLimitPerMinute,
        IReadOnlyList<string> Endpoints);
}
