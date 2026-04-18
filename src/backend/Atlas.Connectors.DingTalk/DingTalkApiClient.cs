using System.Net.Http.Headers;
using System.Net.Http.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Caching;
using Atlas.Connectors.DingTalk.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Connectors.DingTalk;

/// <summary>
/// 钉钉 OpenAPI 客户端。负责：
/// 1. IHttpClientFactory 命名 dingtalk-api 拿 HttpClient；
/// 2. IConnectorTokenCache 缓存 access_token（走 v1.0 的 oauth2/accessToken）；
/// 3. 提供 v1 老版（errcode 样式）GET/POST 与 v1.0 新版（Bearer + errcode 样式 result）GET/POST。
/// </summary>
public sealed class DingTalkApiClient
{
    public const string HttpClientName = "dingtalk-api";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConnectorTokenCache _tokenCache;
    private readonly IConnectorRuntimeOptionsResolver<DingTalkRuntimeOptions> _runtimeResolver;
    private readonly DingTalkOptions _options;
    private readonly ILogger<DingTalkApiClient> _logger;

    public DingTalkApiClient(
        IHttpClientFactory httpClientFactory,
        IConnectorTokenCache tokenCache,
        IConnectorRuntimeOptionsResolver<DingTalkRuntimeOptions> runtimeResolver,
        IOptions<DingTalkOptions> options,
        ILogger<DingTalkApiClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _tokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));
        _runtimeResolver = runtimeResolver ?? throw new ArgumentNullException(nameof(runtimeResolver));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<DingTalkRuntimeOptions> ResolveRuntimeOptionsAsync(ConnectorContext context, CancellationToken cancellationToken)
        => _runtimeResolver.ResolveAsync(context, cancellationToken);

    public async Task<string> GetAccessTokenAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        var runtime = await _runtimeResolver.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var cacheKey = BuildAccessTokenCacheKey(context.TenantId, context.ProviderInstanceId);

        var cached = await _tokenCache.GetOrCreateAsync(cacheKey, async ct =>
        {
            var token = await AcquireAccessTokenAsync(runtime, ct).ConfigureAwait(false);
            var ttl = TimeSpan.FromSeconds(Math.Max(60, token.ExpireIn - _options.AccessTokenSafetyMarginSeconds));
            return (new DingTalkCachedToken(token.AccessToken!, DateTimeOffset.UtcNow.Add(ttl)), ttl);
        }, cancellationToken).ConfigureAwait(false);

        return cached.AccessToken;
    }

    public async Task InvalidateAccessTokenAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        var cacheKey = BuildAccessTokenCacheKey(context.TenantId, context.ProviderInstanceId);
        await _tokenCache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 走 v1 老版 OpenAPI 的 POST JSON（oapi.dingtalk.com，走 ?access_token= 方式）。
    /// </summary>
    internal async Task<TResponse> SendLegacyPostJsonAsync<TBody, TResponse>(
        ConnectorContext context,
        string relativePath,
        TBody body,
        CancellationToken cancellationToken) where TResponse : DingTalkLegacyResponse, new()
    {
        var token = await GetAccessTokenAsync(context, cancellationToken).ConfigureAwait(false);
        var url = BuildLegacyUrlWithToken(relativePath, token);
        var client = CreateClient();
        using var resp = await client.PostAsJsonAsync(url, body, cancellationToken).ConfigureAwait(false);
        return await ReadLegacyAsync<TResponse>(resp, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>走 v1 老版 OpenAPI 的 GET（ping / 查询类），?access_token= 方式。</summary>
    internal async Task<TResponse> SendLegacyGetAsync<TResponse>(
        ConnectorContext context,
        string relativePath,
        IDictionary<string, string>? extraQuery,
        CancellationToken cancellationToken) where TResponse : DingTalkLegacyResponse, new()
    {
        var token = await GetAccessTokenAsync(context, cancellationToken).ConfigureAwait(false);
        var url = BuildLegacyUrlWithToken(relativePath, token, extraQuery);
        var client = CreateClient();
        using var resp = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        return await ReadLegacyAsync<TResponse>(resp, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 走 v1.0 新版 OpenAPI 的 POST JSON（api.dingtalk.com，x-acs-dingtalk-access-token header）。
    /// 新版响应不强制 errcode 结构，调用方按实际 DTO 反序列化。
    /// </summary>
    internal async Task<TResponse> SendV1PostJsonAsync<TBody, TResponse>(
        ConnectorContext context,
        string relativePath,
        TBody body,
        CancellationToken cancellationToken) where TResponse : class
    {
        var token = await GetAccessTokenAsync(context, cancellationToken).ConfigureAwait(false);
        var client = CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_options.ApiBaseUrl}{relativePath}")
        {
            Content = JsonContent.Create(body),
        };
        req.Headers.TryAddWithoutValidation("x-acs-dingtalk-access-token", token);
        using var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var payload = await resp.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new ConnectorException(ConnectorErrorCodes.Unknown, "DingTalk v1.0 response body empty.", DingTalkConnectorMarker.ProviderType);
        return payload;
    }

    /// <summary>走 v1.0 新版 OpenAPI 的 GET。</summary>
    internal async Task<TResponse> SendV1GetAsync<TResponse>(
        ConnectorContext context,
        string relativePath,
        CancellationToken cancellationToken) where TResponse : class
    {
        var token = await GetAccessTokenAsync(context, cancellationToken).ConfigureAwait(false);
        var client = CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{_options.ApiBaseUrl}{relativePath}");
        req.Headers.TryAddWithoutValidation("x-acs-dingtalk-access-token", token);
        using var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var payload = await resp.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new ConnectorException(ConnectorErrorCodes.Unknown, "DingTalk v1.0 response body empty.", DingTalkConnectorMarker.ProviderType);
        return payload;
    }

    /// <summary>
    /// OAuth2 用户登录码换 accessToken + 用户身份：走 v1.0 /v1.0/oauth2/userAccessToken
    /// body: { clientId, clientSecret, code, grantType = authorization_code }.
    /// </summary>
    internal async Task<DingTalkUserAccessTokenData> ExchangeUserAccessTokenAsync(
        ConnectorContext context,
        string code,
        CancellationToken cancellationToken)
    {
        var runtime = await _runtimeResolver.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var body = new
        {
            clientId = runtime.AppKey,
            clientSecret = runtime.AppSecret,
            code,
            grantType = "authorization_code",
        };
        var url = $"{_options.ApiBaseUrl}/v1.0/oauth2/userAccessToken";
        var client = CreateClient();
        using var resp = await client.PostAsJsonAsync(url, body, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var payload = await resp.Content.ReadFromJsonAsync<DingTalkUserAccessTokenData>(cancellationToken: cancellationToken).ConfigureAwait(false);

        if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
        {
            throw new ConnectorException(ConnectorErrorCodes.OAuthCodeInvalid, "DingTalk userAccessToken exchange returned empty token.", DingTalkConnectorMarker.ProviderType);
        }
        return payload;
    }

    /// <summary>
    /// 携带 user-level Bearer（x-acs-dingtalk-access-token）发 v1.0 GET。用于 OAuth 登录后换取 contact/users/me 等。
    /// </summary>
    internal async Task<TResponse> SendV1UserGetAsync<TResponse>(string userAccessToken, string relativePath, CancellationToken cancellationToken) where TResponse : class
    {
        if (string.IsNullOrWhiteSpace(userAccessToken))
        {
            throw new ConnectorException(ConnectorErrorCodes.TokenInvalid, "DingTalk user access token is empty.", DingTalkConnectorMarker.ProviderType);
        }

        var client = CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{_options.ApiBaseUrl}{relativePath}");
        req.Headers.TryAddWithoutValidation("x-acs-dingtalk-access-token", userAccessToken);
        using var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var payload = await resp.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new ConnectorException(ConnectorErrorCodes.Unknown, "DingTalk v1.0 user GET response empty.", DingTalkConnectorMarker.ProviderType);
        return payload;
    }

    private async Task<DingTalkAccessTokenResponse> AcquireAccessTokenAsync(DingTalkRuntimeOptions runtime, CancellationToken cancellationToken)
    {
        // v1.0 /v1.0/oauth2/accessToken 返回 { accessToken, expireIn }。
        var url = $"{_options.ApiBaseUrl}/v1.0/oauth2/accessToken";
        var body = new { appKey = runtime.AppKey, appSecret = runtime.AppSecret };
        var client = CreateClient();
        try
        {
            using var resp = await client.PostAsJsonAsync(url, body, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var payload = await resp.Content.ReadFromJsonAsync<DingTalkAccessTokenResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
            {
                throw new ConnectorException(ConnectorErrorCodes.TokenAcquireFailed, "DingTalk accessToken response empty.", DingTalkConnectorMarker.ProviderType);
            }
            return payload;
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not ConnectorException)
        {
            _logger.LogWarning(ex, "DingTalk accessToken transport error for appKey {AppKey}.", runtime.AppKey);
            throw new ConnectorException(ConnectorErrorCodes.TokenAcquireFailed, "DingTalk accessToken transport error.", DingTalkConnectorMarker.ProviderType, innerException: ex);
        }
    }

    private string BuildLegacyUrlWithToken(string relativePath, string accessToken, IDictionary<string, string>? extraQuery = null)
    {
        var sep = relativePath.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var sb = new System.Text.StringBuilder()
            .Append(_options.LegacyApiBaseUrl)
            .Append(relativePath)
            .Append(sep)
            .Append("access_token=")
            .Append(Uri.EscapeDataString(accessToken));
        if (extraQuery is { Count: > 0 })
        {
            foreach (var (k, v) in extraQuery)
            {
                sb.Append('&').Append(Uri.EscapeDataString(k)).Append('=').Append(Uri.EscapeDataString(v));
            }
        }
        return sb.ToString();
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromMilliseconds(_options.DefaultRequestTimeoutMs);
        return client;
    }

    private static async Task<TResponse> ReadLegacyAsync<TResponse>(HttpResponseMessage response, CancellationToken cancellationToken)
        where TResponse : DingTalkLegacyResponse, new()
    {
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new ConnectorException(ConnectorErrorCodes.Unknown, "DingTalk legacy response body empty.", DingTalkConnectorMarker.ProviderType);

        if (payload.ErrCode != 0)
        {
            var code = DingTalkErrorMapper.MapErrorCode(payload.ErrCode);
            throw new ConnectorException(code, payload.ErrMsg ?? "DingTalk api error", DingTalkConnectorMarker.ProviderType, payload.ErrCode, payload.ErrMsg);
        }
        return payload;
    }

    private static string BuildAccessTokenCacheKey(Guid tenantId, long providerInstanceId)
        => $"connector:dingtalk:{tenantId:D}:{providerInstanceId}:access_token";
}

internal sealed record DingTalkCachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

/// <summary>v1.0 /oauth2/userAccessToken 响应。</summary>
internal sealed class DingTalkUserAccessTokenData
{
    [System.Text.Json.Serialization.JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("expireIn")]
    public int ExpireIn { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("corpId")]
    public string? CorpId { get; set; }
}

internal static class DingTalkErrorMapper
{
    public static string MapErrorCode(int errcode) => errcode switch
    {
        0 => "ok",
        // OAuth: 40078 (bad code), 40088 (bad redirect)
        40078 or 40088 => ConnectorErrorCodes.OAuthCodeInvalid,
        // 可见范围/通讯录权限不足
        60011 or 60020 or 60121 or 60122 => ConnectorErrorCodes.VisibilityScopeDenied,
        // access_token 失效
        42001 or 41001 or 40014 => ConnectorErrorCodes.TokenExpired,
        // 用户不存在
        60004 or 60104 => ConnectorErrorCodes.IdentityNotFound,
        // 审批参数错误 / 创建失败
        301007 or 301008 or 301010 => ConnectorErrorCodes.ApprovalSubmitFailed,
        _ => ConnectorErrorCodes.Unknown,
    };
}
