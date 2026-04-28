using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Caching;
using Atlas.Connectors.Feishu.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Connectors.Feishu;

/// <summary>
/// 飞书 OpenAPI 客户端。注册为 Singleton，无任何 Scoped 依赖。
/// 调用方（Application 层）须先通过 IConnectorRuntimeOptionsAccessor 解析 FeishuRuntimeOptions，
/// 再塞进 ConnectorContext.RuntimeOptions 传入；本客户端只负责：
/// 1. 通过 IHttpClientFactory 命名 feishu-api 拿 HttpClient；
/// 2. tenant_access_token / user_access_token 缓存与刷新；
/// 3. 提供低层 SendTenantGet / SendTenantPost / SendUserGet 方法。
/// </summary>
public sealed class FeishuApiClient
{
    public const string HttpClientName = "feishu-api";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConnectorTokenCache _tokenCache;
    private readonly FeishuOptions _options;
    private readonly ILogger<FeishuApiClient> _logger;

    public FeishuApiClient(
        IHttpClientFactory httpClientFactory,
        IConnectorTokenCache tokenCache,
        IOptions<FeishuOptions> options,
        ILogger<FeishuApiClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _tokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 从 ConnectorContext.RuntimeOptions 强类型 cast 出 FeishuRuntimeOptions。
    /// 类型不匹配（即调用方未先经 Application 层 Accessor 解析）抛 ProviderConfigInvalid。
    /// </summary>
    public static FeishuRuntimeOptions ResolveRuntime(ConnectorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.RuntimeOptions is not FeishuRuntimeOptions runtime)
        {
            throw new ConnectorException(
                ConnectorErrorCodes.ProviderConfigInvalid,
                $"ConnectorContext.RuntimeOptions is not FeishuRuntimeOptions (actual: {context.RuntimeOptions?.GetType().FullName ?? "null"}). Caller must resolve runtime via IConnectorRuntimeOptionsAccessor before invoking Feishu provider.",
                FeishuConnectorMarker.ProviderType);
        }
        return runtime;
    }

    public async Task<string> GetTenantAccessTokenAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        var runtime = ResolveRuntime(context);
        var cacheKey = BuildTenantTokenCacheKey(context.TenantId, context.ProviderInstanceId, runtime.GetCredentialFingerprint());

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
        var runtime = ResolveRuntime(context);
        var cacheKey = BuildTenantTokenCacheKey(context.TenantId, context.ProviderInstanceId, runtime.GetCredentialFingerprint());
        await _tokenCache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 通过 OAuth code 换取 user_access_token。
    /// 注意：Feishu authen/v2/oauth/token 不回传 open_id；调用方应在拿到 access_token 后走 user_info 解析身份，
    /// 再通过 <see cref="CacheUserAccessTokenAsync"/> 把 (externalUserId → token) 写进缓存。
    /// </summary>
    internal async Task<FeishuUserAccessTokenData> ExchangeUserAccessTokenAsync(
        ConnectorContext context,
        string code,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        var runtime = ResolveRuntime(context);
        var body = new FeishuUserTokenExchangeBody
        {
            GrantType = "authorization_code",
            ClientId = runtime.AppId,
            ClientSecret = runtime.AppSecret,
            Code = code,
            RedirectUri = redirectUri,
        };
        return await PostUserTokenEndpointAsync(body, ConnectorErrorCodes.OAuthCodeInvalid, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 用 refresh_token 刷新 user_access_token。调用方负责在刷新后重新写缓存。
    /// </summary>
    internal async Task<FeishuUserAccessTokenData> RefreshUserAccessTokenAsync(ConnectorContext context, string refreshToken, CancellationToken cancellationToken)
    {
        var runtime = ResolveRuntime(context);
        var body = new FeishuUserTokenExchangeBody
        {
            GrantType = "refresh_token",
            ClientId = runtime.AppId,
            ClientSecret = runtime.AppSecret,
            RefreshToken = refreshToken,
        };
        return await PostUserTokenEndpointAsync(body, ConnectorErrorCodes.TokenExpired, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 写入 user_access_token 缓存。key=(tenantId, providerInstanceId, externalUserId)。
    /// access_token TTL = expires_in - UserTokenSafetyMarginSeconds；总缓存条目 TTL = refresh_token 剩余时间。
    /// 同一程序集内使用（Internal：不暴露 FeishuUserAccessTokenData 内部模型）。
    /// </summary>
    internal async Task CacheUserAccessTokenAsync(ConnectorContext context, string externalUserId, FeishuUserAccessTokenData data, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (string.IsNullOrWhiteSpace(externalUserId) || string.IsNullOrEmpty(data.AccessToken))
        {
            return;
        }

        var runtime = ResolveRuntime(context);
        var now = DateTimeOffset.UtcNow;
        var accessTtlSeconds = Math.Max(60, data.ExpiresIn - _options.UserTokenSafetyMarginSeconds);
        var refreshExpiresAtUtc = data.RefreshExpiresIn > 0 ? now.AddSeconds(data.RefreshExpiresIn) : now.AddDays(30);
        var cached = new FeishuCachedUserToken(
            data.AccessToken!,
            data.RefreshToken,
            now.AddSeconds(accessTtlSeconds),
            refreshExpiresAtUtc,
            externalUserId);

        var cacheKey = BuildUserTokenCacheKey(context.TenantId, context.ProviderInstanceId, runtime.GetCredentialFingerprint(), externalUserId);
        var entryTtl = refreshExpiresAtUtc - now;
        if (entryTtl <= TimeSpan.Zero)
        {
            entryTtl = TimeSpan.FromSeconds(accessTtlSeconds);
        }
        await _tokenCache.SetAsync(cacheKey, cached, entryTtl, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 读取缓存的 user_access_token；若 access_token 处于安全窗内直接返回，
    /// 到期但 refresh_token 仍有效时自动刷新并回写缓存；若两者都失效返回 null 让调用方走 OAuth 重走。
    /// </summary>
    public async Task<string?> GetCachedUserAccessTokenAsync(ConnectorContext context, string externalUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            return null;
        }

        var runtime = ResolveRuntime(context);
        var cacheKey = BuildUserTokenCacheKey(context.TenantId, context.ProviderInstanceId, runtime.GetCredentialFingerprint(), externalUserId);
        var cached = await _tokenCache.GetAsync<FeishuCachedUserToken>(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached is null)
        {
            return null;
        }

        if (cached.RefreshExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            await _tokenCache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (cached.ExpiresAtUtc > DateTimeOffset.UtcNow.AddSeconds(_options.UserTokenSafetyMarginSeconds))
        {
            return cached.AccessToken;
        }

        if (string.IsNullOrEmpty(cached.RefreshToken))
        {
            return cached.AccessToken; // 无 refresh_token，仍返回旧 token 让调用方自行决定 401 重试。
        }

        try
        {
            var refreshed = await RefreshUserAccessTokenAsync(context, cached.RefreshToken, cancellationToken).ConfigureAwait(false);
            await CacheUserAccessTokenAsync(context, externalUserId, refreshed, cancellationToken).ConfigureAwait(false);
            return refreshed.AccessToken;
        }
        catch (ConnectorException ex)
        {
            _logger.LogInformation(ex, "Feishu user_access_token refresh failed for tenant {TenantId}/instance {ProviderInstanceId}; dropping cache.", context.TenantId, context.ProviderInstanceId);
            await _tokenCache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private async Task<FeishuUserAccessTokenData> PostUserTokenEndpointAsync(FeishuUserTokenExchangeBody body, string failureCode, CancellationToken cancellationToken)
    {
        var url = $"{_options.ApiBaseUrl}/open-apis/authen/v2/oauth/token";

        var client = CreateClient();
        using var resp = await client.PostAsJsonAsync(url, body, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var payload = await resp.Content.ReadFromJsonAsync<FeishuUserAccessTokenResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

        if (payload is null || payload.Code != 0 || payload.Data is null || string.IsNullOrEmpty(payload.Data.AccessToken))
        {
            throw new ConnectorException(
                failureCode,
                $"Feishu user_access_token endpoint failed: code={payload?.Code}, msg={payload?.Msg}",
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

    private static string BuildTenantTokenCacheKey(Guid tenantId, long providerInstanceId, string credentialFingerprint)
        => $"connector:feishu:{tenantId:D}:{providerInstanceId}:{credentialFingerprint}:tenant_access_token";

    private static string BuildUserTokenCacheKey(Guid tenantId, long providerInstanceId, string credentialFingerprint, string externalUserId)
        => $"connector:feishu:{tenantId:D}:{providerInstanceId}:{credentialFingerprint}:user_access_token:{externalUserId}";
}

internal sealed record FeishuCachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// 飞书 user_access_token 缓存载体。同时带 refresh_token 与 refresh_token 过期时间，
/// 让 <see cref="FeishuApiClient.GetCachedUserAccessTokenAsync"/> 可以自动刷新、失败时清缓存。
/// </summary>
internal sealed record FeishuCachedUserToken(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset RefreshExpiresAtUtc,
    string ExternalUserId);

/// <summary>
/// 飞书 authen/v2/oauth/token 请求体。authorization_code / refresh_token 两种 grant_type 共用。
/// </summary>
internal sealed class FeishuUserTokenExchangeBody
{
    [System.Text.Json.Serialization.JsonPropertyName("grant_type")]
    public required string GrantType { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("client_id")]
    public required string ClientId { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("client_secret")]
    public required string ClientSecret { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("code")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("redirect_uri")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? RedirectUri { get; init; }

    [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; init; }
}

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
