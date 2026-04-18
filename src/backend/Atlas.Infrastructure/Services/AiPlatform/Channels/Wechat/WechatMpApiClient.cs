using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Application.AiPlatform.Abstractions.Channels;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Repositories.AiPlatform;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Services.AiPlatform.Channels.Wechat;

/// <summary>
/// 微信公众号开放接口默认实现。
/// </summary>
public sealed class WechatMpApiClient : IWechatMpApiClient
{
    public const string HttpClientName = "wechat-mp-api";
    private const string DefaultBaseUrl = "https://api.weixin.qq.com";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly ConcurrentDictionary<long, TokenSlot> TokenCache = new();
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> RefreshLocks = new();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WechatMpChannelCredentialRepository _credentialRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<WechatMpApiClient> _logger;

    public WechatMpApiClient(
        IHttpClientFactory httpClientFactory,
        WechatMpChannelCredentialRepository credentialRepository,
        ITenantProvider tenantProvider,
        ILogger<WechatMpApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _credentialRepository = credentialRepository;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(long channelId, string appId, string appSecret, CancellationToken cancellationToken)
    {
        if (TokenCache.TryGetValue(channelId, out var cached) &&
            cached.ExpiresAt > DateTime.UtcNow.AddSeconds(60))
        {
            return cached.Token;
        }
        var gate = RefreshLocks.GetOrAdd(channelId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (TokenCache.TryGetValue(channelId, out cached) &&
                cached.ExpiresAt > DateTime.UtcNow.AddSeconds(60))
            {
                return cached.Token;
            }
            var (token, expiresAt) = await FetchTokenAsync(appId, appSecret, cancellationToken);
            TokenCache[channelId] = new TokenSlot(token, expiresAt);
            try
            {
                var tenantId = TryGetTenantId();
                if (tenantId is not null)
                {
                    var entity = await _credentialRepository.FindByChannelAsync(tenantId.Value, channelId, cancellationToken);
                    if (entity is not null)
                    {
                        entity.RecordAccessTokenRefresh(expiresAt);
                        await _credentialRepository.UpdateAsync(entity, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist wechat-mp token refresh metadata (channel={ChannelId})", channelId);
            }
            return token;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SendCustomerMessageAsync(string accessToken, string toUser, string msgType, string content, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        EnsureBaseAddress(client);

        var url = $"/cgi-bin/message/custom/send?access_token={Uri.EscapeDataString(accessToken)}";
        object body = msgType.Equals("text", StringComparison.OrdinalIgnoreCase)
            ? new { touser = toUser, msgtype = "text", text = new { content } }
            : new { touser = toUser, msgtype = msgType, content };

        using var resp = await client.PostAsJsonAsync(url, body, JsonOptions, cancellationToken);
        var raw = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            throw new BusinessException("WECHAT_MP_API_FAILED", $"wechat-mp custom http={(int)resp.StatusCode} body={Trunc(raw, 256)}");
        }
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("errcode", out var ec) && ec.ValueKind == JsonValueKind.Number && ec.GetInt32() != 0)
            {
                var msg = doc.RootElement.TryGetProperty("errmsg", out var em) ? em.GetString() : null;
                throw new BusinessException("WECHAT_MP_API_FAILED", $"wechat-mp errcode={ec.GetInt32()} errmsg={msg}");
            }
        }
        catch (JsonException ex)
        {
            throw new BusinessException("WECHAT_MP_API_FAILED", $"wechat-mp non-json response: {Trunc(raw, 200)} ({ex.Message})");
        }
    }

    private async Task<(string Token, DateTime ExpiresAt)> FetchTokenAsync(string appId, string appSecret, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        EnsureBaseAddress(client);
        var url = $"/cgi-bin/token?grant_type=client_credential&appid={Uri.EscapeDataString(appId)}&secret={Uri.EscapeDataString(appSecret)}";
        using var resp = await client.GetAsync(url, cancellationToken);
        var raw = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            throw new BusinessException("WECHAT_MP_TOKEN_FAILED", $"wechat-mp token http={(int)resp.StatusCode} body={Trunc(raw, 256)}");
        }
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("errcode", out var ec) && ec.ValueKind == JsonValueKind.Number && ec.GetInt32() != 0)
            {
                var msg = doc.RootElement.TryGetProperty("errmsg", out var em) ? em.GetString() : null;
                throw new BusinessException("WECHAT_MP_TOKEN_FAILED", $"wechat-mp errcode={ec.GetInt32()} errmsg={msg}");
            }
            var token = doc.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() : null;
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) && exp.ValueKind == JsonValueKind.Number ? exp.GetInt32() : 7200;
            if (string.IsNullOrEmpty(token))
            {
                throw new BusinessException("WECHAT_MP_TOKEN_FAILED", "wechat-mp empty token");
            }
            return (token!, DateTime.UtcNow.AddSeconds(expiresIn));
        }
        catch (JsonException ex)
        {
            throw new BusinessException("WECHAT_MP_TOKEN_FAILED", $"wechat-mp non-json response: {Trunc(raw, 200)} ({ex.Message})");
        }
    }

    private static void EnsureBaseAddress(HttpClient client)
    {
        if (client.BaseAddress is null)
        {
            client.BaseAddress = new Uri(DefaultBaseUrl);
        }
        if (client.Timeout == default || client.Timeout > TimeSpan.FromSeconds(60))
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        }
    }

    private TenantId? TryGetTenantId()
    {
        try
        {
            return _tenantProvider.GetTenantId();
        }
        catch
        {
            return null;
        }
    }

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max];

    private sealed record TokenSlot(string Token, DateTime ExpiresAt);
}
