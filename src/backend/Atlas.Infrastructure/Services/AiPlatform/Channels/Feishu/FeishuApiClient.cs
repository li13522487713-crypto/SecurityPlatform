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

namespace Atlas.Infrastructure.Services.AiPlatform.Channels.Feishu;

/// <summary>
/// 飞书开放平台 API 客户端默认实现。
/// 行为：
/// - <c>GetTenantAccessTokenAsync</c> 走内存缓存 + per-channel SemaphoreSlim，命中且未到期直接返回；
///   首次拉取或将到期 60 秒前才会真正向飞书发起 HTTP 请求；
///   成功后写库更新 <c>FeishuChannelCredential.TenantAccessTokenExpiresAt + RefreshCount</c>。
/// - <c>SendImMessageAsync</c> POST /open-apis/im/v1/messages?receive_id_type=...，
///   响应 code != 0 时抛 <see cref="BusinessException"/>，方便上层 connector 统一审计。
/// </summary>
public sealed class FeishuApiClient : IFeishuApiClient
{
    public const string HttpClientName = "feishu-api";
    private const string DefaultBaseUrl = "https://open.feishu.cn";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly ConcurrentDictionary<long, TokenSlot> TokenCache = new();
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> RefreshLocks = new();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly FeishuChannelCredentialRepository _credentialRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<FeishuApiClient> _logger;

    public FeishuApiClient(
        IHttpClientFactory httpClientFactory,
        FeishuChannelCredentialRepository credentialRepository,
        ITenantProvider tenantProvider,
        ILogger<FeishuApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _credentialRepository = credentialRepository;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<string> GetTenantAccessTokenAsync(
        long channelId,
        string appId,
        string appSecret,
        CancellationToken cancellationToken)
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

            // 持久化刷新元数据；找不到凭据条目时只记日志（防御性）
            try
            {
                var tenantId = TryGetTenantId();
                if (tenantId is not null)
                {
                    var entity = await _credentialRepository.FindByChannelAsync(tenantId.Value, channelId, cancellationToken);
                    if (entity is not null)
                    {
                        entity.RecordTokenRefresh(expiresAt);
                        await _credentialRepository.UpdateAsync(entity, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist feishu token refresh metadata (channel={ChannelId})", channelId);
            }

            return token;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SendImMessageAsync(
        string tenantAccessToken,
        string receiveIdType,
        string receiveId,
        string msgType,
        string content,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        EnsureBaseAddress(client);

        var url = $"/open-apis/im/v1/messages?receive_id_type={Uri.EscapeDataString(receiveIdType)}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tenantAccessToken);
        req.Content = JsonContent.Create(new
        {
            receive_id = receiveId,
            msg_type = msgType,
            content
        });

        using var resp = await client.SendAsync(req, cancellationToken);
        var bodyJson = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            throw new BusinessException("FEISHU_API_FAILED", $"feishu im.send http={(int)resp.StatusCode} body={Trunc(bodyJson, 256)}");
        }

        try
        {
            using var doc = JsonDocument.Parse(bodyJson);
            if (doc.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number && code.GetInt32() != 0)
            {
                var msg = doc.RootElement.TryGetProperty("msg", out var m) ? m.GetString() : null;
                throw new BusinessException("FEISHU_API_FAILED", $"feishu code={code.GetInt32()} msg={msg}");
            }
        }
        catch (JsonException ex)
        {
            throw new BusinessException("FEISHU_API_FAILED", $"feishu non-json response: {Trunc(bodyJson, 200)} ({ex.Message})");
        }
    }

    private async Task<(string Token, DateTime ExpiresAt)> FetchTokenAsync(string appId, string appSecret, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        EnsureBaseAddress(client);

        using var resp = await client.PostAsJsonAsync(
            "/open-apis/auth/v3/tenant_access_token/internal",
            new { app_id = appId, app_secret = appSecret },
            JsonOptions,
            cancellationToken);
        var bodyJson = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            throw new BusinessException("FEISHU_TOKEN_FAILED", $"feishu token http={(int)resp.StatusCode} body={Trunc(bodyJson, 256)}");
        }

        try
        {
            using var doc = JsonDocument.Parse(bodyJson);
            var code = doc.RootElement.TryGetProperty("code", out var c) ? c.GetInt32() : -1;
            if (code != 0)
            {
                var msg = doc.RootElement.TryGetProperty("msg", out var m) ? m.GetString() : null;
                throw new BusinessException("FEISHU_TOKEN_FAILED", $"feishu code={code} msg={msg}");
            }
            var token = doc.RootElement.GetProperty("tenant_access_token").GetString();
            var expireSeconds = doc.RootElement.TryGetProperty("expire", out var exp) && exp.ValueKind == JsonValueKind.Number
                ? exp.GetInt32()
                : 7200;
            if (string.IsNullOrEmpty(token))
            {
                throw new BusinessException("FEISHU_TOKEN_FAILED", "feishu empty token");
            }
            return (token!, DateTime.UtcNow.AddSeconds(expireSeconds));
        }
        catch (JsonException ex)
        {
            throw new BusinessException("FEISHU_TOKEN_FAILED", $"feishu non-json response: {Trunc(bodyJson, 200)} ({ex.Message})");
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
