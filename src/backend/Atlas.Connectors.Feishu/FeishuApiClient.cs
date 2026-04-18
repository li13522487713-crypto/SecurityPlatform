using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Caching;
using Atlas.Connectors.Feishu.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Connectors.Feishu;

/// <summary>
/// 飞书 OpenAPI 客户端。负责：
/// 1. 通过 IHttpClientFactory 命名 feishu-api 拿 HttpClient；
/// 2. tenant_access_token 缓存与刷新；
/// 3. 提供低层 SendTenantGet / SendTenantPost / SendUserGet 方法。
/// </summary>
public sealed class FeishuApiClient
{
    public const string HttpClientName = "feishu-api";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConnectorTokenCache _tokenCache;
    private readonly IConnectorRuntimeOptionsResolver<FeishuRuntimeOptions> _runtimeResolver;
    private readonly FeishuOptions _options;
    private readonly ILogger<FeishuApiClient> _logger;

    public FeishuApiClient(
        IHttpClientFactory httpClientFactory,
        IConnectorTokenCache tokenCache,
        IConnectorRuntimeOptionsResolver<FeishuRuntimeOptions> runtimeResolver,
        IOptions<FeishuOptions> options,
        ILogger<FeishuApiClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _tokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));
        _runtimeResolver = runtimeResolver ?? throw new ArgumentNullException(nameof(runtimeResolver));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<FeishuRuntimeOptions> ResolveRuntimeOptionsAsync(ConnectorContext context, CancellationToken cancellationToken)
        => _runtimeResolver.ResolveAsync(context, cancellationToken);

    public async Task<string> GetTenantAccessTokenAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        var runtime = await _runtimeResolver.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var cacheKey = BuildTenantTokenCacheKey(context.TenantId, context.ProviderInstanceId);

        var cached = await _tokenCache.GetOrCreateAsync(cacheKey, async ct =>
        {
            var token = await AcquireTenantAccessTokenAsync(runtime, ct).ConfigureAwait(false);
            var ttl = TimeSpan.FromSeconds(Math.Max(60, token.Expire - _options.TenantTokenSafetyMarginSeconds));
            return (new FeishuCachedToken(token.TenantAccessToken!, DateTimeOffset.UtcNow.Add(ttl)), ttl);
        }, cancellationToken).ConfigureAwait(false);

        return cached.AccessToken;
    }

    public async Task InvalidateTenantAccessTokenAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        var cacheKey = BuildTenantTokenCacheKey(context.TenantId, context.ProviderInstanceId);
        await _tokenCache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 通过 OAuth code 换取 user_access_token；不缓存（用户级 token 由调用方按需刷新）。
    /// internal 暴露给同程序集 Provider 使用。
    /// </summary>
    internal async Task<FeishuUserAccessTokenData> ExchangeUserAccessTokenAsync(
        ConnectorContext context,
        string code,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        var runtime = await _runtimeResolver.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var url = $"{_options.ApiBaseUrl}/open-apis/authen/v2/oauth/token";
        var body = new
        {
            grant_type = "authorization_code",
            client_id = runtime.AppId,
            client_secret = runtime.AppSecret,
            code,
            redirect_uri = redirectUri,
        };

        var client = CreateClient();
        using var resp = await client.PostAsJsonAsync(url, body, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var payload = await resp.Content.ReadFromJsonAsync<FeishuUserAccessTokenResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

        if (payload is null || payload.Code != 0 || payload.Data is null || string.IsNullOrEmpty(payload.Data.AccessToken))
        {
            throw new ConnectorException(
                ConnectorErrorCodes.OAuthCodeInvalid,
                $"Feishu user_access_token exchange failed: code={payload?.Code}, msg={payload?.Msg}",
                FeishuConnectorMarker.ProviderType,
                payload?.Code,
                payload?.Msg);
        }

        return payload.Data;
    }

    /// <summary>
    /// 用 refresh_token 刷新 user_access_token。
    /// </summary>
    internal async Task<FeishuUserAccessTokenData> RefreshUserAccessTokenAsync(ConnectorContext context, string refreshToken, CancellationToken cancellationToken)
    {
        var runtime = await _runtimeResolver.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var url = $"{_options.ApiBaseUrl}/open-apis/authen/v2/oauth/token";
        var body = new
        {
            grant_type = "refresh_token",
            client_id = runtime.AppId,
            client_secret = runtime.AppSecret,
            refresh_token = refreshToken,
        };

        var client = CreateClient();
        using var resp = await client.PostAsJsonAsync(url, body, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var payload = await resp.Content.ReadFromJsonAsync<FeishuUserAccessTokenResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

        if (payload is null || payload.Code != 0 || payload.Data is null || string.IsNullOrEmpty(payload.Data.AccessToken))
        {
            throw new ConnectorException(
                ConnectorErrorCodes.TokenExpired,
                $"Feishu refresh_token failed: code={payload?.Code}, msg={payload?.Msg}",
                FeishuConnectorMarker.ProviderType,
                payload?.Code,
                payload?.Msg);
        }

        return payload.Data;
    }

    /// <summary>
    /// 用 user_access_token 拉用户信息（authen/v1/user_info）。
    /// </summary>
    internal async Task<FeishuUserInfoData> GetUserInfoAsync(string userAccessToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userAccessToken))
        {
            throw new ConnectorException(ConnectorErrorCodes.IdentityNotFound, "Feishu user_access_token missing.", FeishuConnectorMarker.ProviderType);
        }

        var url = $"{_options.ApiBaseUrl}/open-apis/authen/v1/user_info";
        var client = CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAccessToken);
        using var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var payload = await resp.Content.ReadFromJsonAsync<FeishuApiResponse<FeishuUserInfoData>>(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (payload is null || payload.Code != 0 || payload.Data is null)
        {
            throw new ConnectorException(
                ConnectorErrorCodes.IdentityNotFound,
                $"Feishu user_info failed: code={payload?.Code}, msg={payload?.Msg}",
                FeishuConnectorMarker.ProviderType,
                payload?.Code,
                payload?.Msg);
        }
        return payload.Data;
    }

    /// <summary>
    /// 通过手机号或邮箱批量获取 user_id（contact/v3/users/batch_get_id）。
    /// 返回 key=输入值（手机号/邮箱），value=外部 user_id；找不到的键不写入字典。
    /// 默认 user_id_type 取 FeishuOptions.DefaultUserIdType。
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> BatchGetUserIdsAsync(
        ConnectorContext context,
        IReadOnlyList<string>? mobiles,
        IReadOnlyList<string>? emails,
        string? userIdType,
        CancellationToken cancellationToken)
    {
        var idType = string.IsNullOrWhiteSpace(userIdType) ? _options.DefaultUserIdType : userIdType;
        var path = $"/open-apis/contact/v3/users/batch_get_id?user_id_type={idType}";
        var body = new
        {
            mobiles = mobiles?.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>(),
            emails = emails?.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? Array.Empty<string>(),
        };

        var resp = await SendTenantPostAsync<object, FeishuBatchGetIdData>(context, path, body, cancellationToken).ConfigureAwait(false);
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (resp.Data?.UserList is { Length: > 0 } list)
        {
            foreach (var entry in list)
            {
                if (string.IsNullOrEmpty(entry.UserId))
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(entry.Mobile))
                {
                    dict[entry.Mobile!] = entry.UserId!;
                }
                if (!string.IsNullOrEmpty(entry.Email))
                {
                    dict[entry.Email!] = entry.UserId!;
                }
            }
        }
        return dict;
    }

    /// <summary>
    /// 走 tenant_access_token 的 GET。errcode 非 0 抛 ConnectorException。
    /// </summary>
    internal async Task<FeishuApiResponse<TData>> SendTenantGetAsync<TData>(ConnectorContext context, string relativePath, CancellationToken cancellationToken)
    {
        var token = await GetTenantAccessTokenAsync(context, cancellationToken).ConfigureAwait(false);
        var client = CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{_options.ApiBaseUrl}{relativePath}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        return await ReadResponseAsync<TData>(resp, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 走 tenant_access_token 的 POST JSON。
    /// </summary>
    internal async Task<FeishuApiResponse<TData>> SendTenantPostAsync<TBody, TData>(ConnectorContext context, string relativePath, TBody body, CancellationToken cancellationToken)
    {
        var token = await GetTenantAccessTokenAsync(context, cancellationToken).ConfigureAwait(false);
        var client = CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_options.ApiBaseUrl}{relativePath}")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        return await ReadResponseAsync<TData>(resp, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 走 tenant_access_token 的 PATCH JSON（卡片更新等场景）。
    /// </summary>
    internal async Task<FeishuApiResponse<TData>> SendTenantPatchAsync<TBody, TData>(ConnectorContext context, string relativePath, TBody body, CancellationToken cancellationToken)
    {
        var token = await GetTenantAccessTokenAsync(context, cancellationToken).ConfigureAwait(false);
        var client = CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Patch, $"{_options.ApiBaseUrl}{relativePath}")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        return await ReadResponseAsync<TData>(resp, cancellationToken).ConfigureAwait(false);
    }

    private async Task<FeishuApiResponse<TData>> ReadResponseAsync<TData>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<FeishuApiResponse<TData>>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new ConnectorException(ConnectorErrorCodes.Unknown, "Feishu returned empty body.", FeishuConnectorMarker.ProviderType);

        if (payload.Code != 0)
        {
            var mapped = FeishuErrorMapper.MapErrorCode(payload.Code);
            throw new ConnectorException(mapped, payload.Msg ?? "Feishu api error", FeishuConnectorMarker.ProviderType, payload.Code, payload.Msg);
        }

        return payload;
    }

    private async Task<FeishuTenantAccessTokenResponse> AcquireTenantAccessTokenAsync(FeishuRuntimeOptions runtime, CancellationToken cancellationToken)
    {
        var url = $"{_options.ApiBaseUrl}/open-apis/auth/v3/tenant_access_token/internal";
        var body = new { app_id = runtime.AppId, app_secret = runtime.AppSecret };
        var client = CreateClient();
        try
        {
            using var resp = await client.PostAsJsonAsync(url, body, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var payload = await resp.Content.ReadFromJsonAsync<FeishuTenantAccessTokenResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (payload is null || payload.Code != 0 || string.IsNullOrEmpty(payload.TenantAccessToken))
            {
                throw new ConnectorException(
                    ConnectorErrorCodes.TokenAcquireFailed,
                    $"Feishu tenant_access_token failed: code={payload?.Code}, msg={payload?.Msg}",
                    FeishuConnectorMarker.ProviderType,
                    payload?.Code,
                    payload?.Msg);
            }
            return payload;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ConnectorException)
        {
            _logger.LogWarning(ex, "Feishu tenant_access_token transport error.");
            throw new ConnectorException(ConnectorErrorCodes.TokenAcquireFailed, "Feishu tenant_access_token transport error.", FeishuConnectorMarker.ProviderType, innerException: ex);
        }
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromMilliseconds(_options.DefaultRequestTimeoutMs);
        return client;
    }

    private static string BuildTenantTokenCacheKey(Guid tenantId, long providerInstanceId)
        => $"connector:feishu:{tenantId:D}:{providerInstanceId}:tenant_access_token";
}

internal sealed record FeishuCachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

internal static class FeishuErrorMapper
{
    public static string MapErrorCode(int code) => code switch
    {
        0 => "ok",
        99991663 or 99991664 => ConnectorErrorCodes.TokenAcquireFailed,
        99991668 or 99991671 => ConnectorErrorCodes.OAuthCodeInvalid,
        // 通讯录 V3 权限相关：访问超出应用通讯录权限范围 / 未授予字段权限
        99992402 or 99992433 => ConnectorErrorCodes.VisibilityScopeDenied,
        // 审批相关：90001-90099 通常为审批参数错误
        >= 90001 and <= 90099 => ConnectorErrorCodes.ApprovalSubmitFailed,
        _ => ConnectorErrorCodes.Unknown,
    };
}
