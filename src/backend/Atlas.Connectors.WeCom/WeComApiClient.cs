using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Caching;
using Atlas.Connectors.WeCom.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Connectors.WeCom;

/// <summary>
/// 企微 API 客户端。注册为 Singleton，无任何 Scoped 依赖。
/// 调用方（Application 层）须先通过 IConnectorRuntimeOptionsAccessor 解析 WeComRuntimeOptions，
/// 然后塞进 ConnectorContext.RuntimeOptions 传入；本客户端只负责：
/// 1. 通过 IHttpClientFactory 命名 wecom-api 拿 HttpClient；
/// 2. 通过 IConnectorTokenCache 缓存 access_token；
/// 3. 提供身份 / 通讯录 / 审批 / 消息相关的低层 GET/POST 方法（具体业务封装由 4 个 Provider 调用）。
/// </summary>
public sealed class WeComApiClient
{
    public const string HttpClientName = "wecom-api";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConnectorTokenCache _tokenCache;
    private readonly WeComOptions _options;
    private readonly ILogger<WeComApiClient> _logger;

    public WeComApiClient(
        IHttpClientFactory httpClientFactory,
        IConnectorTokenCache tokenCache,
        IOptions<WeComOptions> options,
        ILogger<WeComApiClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _tokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 从 ConnectorContext.RuntimeOptions 强类型 cast 出 WeComRuntimeOptions。
    /// 类型不匹配（即调用方未先经 Application 层 Accessor 解析）抛 ProviderConfigInvalid。
    /// </summary>
    public static WeComRuntimeOptions ResolveRuntime(ConnectorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.RuntimeOptions is not WeComRuntimeOptions runtime)
        {
            throw new ConnectorException(
                ConnectorErrorCodes.ProviderConfigInvalid,
                $"ConnectorContext.RuntimeOptions is not WeComRuntimeOptions (actual: {context.RuntimeOptions?.GetType().FullName ?? "null"}). Caller must resolve runtime via IConnectorRuntimeOptionsAccessor before invoking WeCom provider.",
                WeComConnectorMarker.ProviderType);
        }
        return runtime;
    }

    /// <summary>
    /// 拿到当前 provider 实例的 access_token。同一进程的并发请求会通过 IConnectorTokenCache 串行刷新。
    /// </summary>
    public async Task<string> GetAccessTokenAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        var runtime = ResolveRuntime(context);
        var cacheKey = BuildAccessTokenCacheKey(context.TenantId, context.ProviderInstanceId, runtime.GetCredentialFingerprint());

        return await _tokenCache.GetOrCreateAsync(cacheKey, async ct =>
        {
            var token = await AcquireAccessTokenAsync(runtime, ct).ConfigureAwait(false);
            var ttl = TimeSpan.FromSeconds(Math.Max(60, token.ExpiresIn - _options.AccessTokenSafetyMarginSeconds));
            var box = new WeComCachedToken(token.AccessToken!, DateTimeOffset.UtcNow.Add(ttl));
            return (box, ttl);
        }, cancellationToken).ConfigureAwait(false) is { } cached
            ? cached.AccessToken
            : throw new ConnectorException(ConnectorErrorCodes.TokenAcquireFailed, "WeCom access_token resolve returned null.", WeComConnectorMarker.ProviderType);
    }

    public async Task InvalidateAccessTokenAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        var runtime = ResolveRuntime(context);
        var cacheKey = BuildAccessTokenCacheKey(context.TenantId, context.ProviderInstanceId, runtime.GetCredentialFingerprint());
        await _tokenCache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 发起一次带 access_token 的 GET，并校验 errcode；errcode 非 0 时抛 ConnectorException。
    /// internal 暴露给同程序集的 Provider 使用，避免泄漏 internal 响应模型给外部。
    /// </summary>
    internal async Task<TResponse> SendAuthorizedGetAsync<TResponse>(
        ConnectorContext context,
        string relativePath,
        IDictionary<string, string>? extraQuery,
        CancellationToken cancellationToken) where TResponse : WeComApiResponse, new()
    {
        var token = await GetAccessTokenAsync(context, cancellationToken).ConfigureAwait(false);
        var url = BuildUrl(relativePath, token, extraQuery);
        return await SendGetAsync<TResponse>(context, url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 发起一次带 access_token 的 POST JSON。
    /// </summary>
    internal async Task<TResponse> SendAuthorizedPostJsonAsync<TBody, TResponse>(
        ConnectorContext context,
        string relativePath,
        TBody body,
        IDictionary<string, string>? extraQuery,
        CancellationToken cancellationToken) where TResponse : WeComApiResponse, new()
    {
        var token = await GetAccessTokenAsync(context, cancellationToken).ConfigureAwait(false);
        var url = BuildUrl(relativePath, token, extraQuery);
        var client = _httpClientFactory.CreateClient(HttpClientName);
        ConfigureClient(client);
        using var response = await client.PostAsJsonAsync(url, body, cancellationToken).ConfigureAwait(false);
        return await ReadAndCheckAsync<TResponse>(response, context, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<TResponse> SendGetAsync<TResponse>(
        ConnectorContext context,
        string fullUrl,
        CancellationToken cancellationToken) where TResponse : WeComApiResponse, new()
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        ConfigureClient(client);
        using var response = await client.GetAsync(fullUrl, cancellationToken).ConfigureAwait(false);
        return await ReadAndCheckAsync<TResponse>(response, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<WeComAccessTokenResponse> AcquireAccessTokenAsync(WeComRuntimeOptions runtime, CancellationToken cancellationToken)
    {
        var url = $"{_options.ApiBaseUrl}/cgi-bin/gettoken?corpid={Uri.EscapeDataString(runtime.CorpId)}&corpsecret={Uri.EscapeDataString(runtime.CorpSecret)}";
        var client = _httpClientFactory.CreateClient(HttpClientName);
        ConfigureClient(client);

        WeComAccessTokenResponse? body;
        try
        {
            body = await client.GetFromJsonAsync<WeComAccessTokenResponse>(url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "WeCom gettoken failed for corp {CorpId}.", runtime.CorpId);
            throw new ConnectorException(ConnectorErrorCodes.TokenAcquireFailed, "WeCom gettoken transport error.", WeComConnectorMarker.ProviderType, innerException: ex);
        }

        if (body is null || string.IsNullOrEmpty(body.AccessToken) || body.ErrCode != 0)
        {
            throw new ConnectorException(
                ConnectorErrorCodes.TokenAcquireFailed,
                $"WeCom gettoken failed: errcode={body?.ErrCode}, errmsg={body?.ErrMsg}",
                WeComConnectorMarker.ProviderType,
                body?.ErrCode,
                body?.ErrMsg);
        }
        return body;
    }

    private string BuildUrl(string relativePath, string accessToken, IDictionary<string, string>? extraQuery)
    {
        var sep = relativePath.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var basePart = $"{_options.ApiBaseUrl}{relativePath}{sep}access_token={Uri.EscapeDataString(accessToken)}";
        if (extraQuery is null || extraQuery.Count == 0)
        {
            return basePart;
        }
        var sb = new System.Text.StringBuilder(basePart);
        foreach (var (k, v) in extraQuery)
        {
            sb.Append('&').Append(Uri.EscapeDataString(k)).Append('=').Append(Uri.EscapeDataString(v));
        }
        return sb.ToString();
    }

    private void ConfigureClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromMilliseconds(_options.DefaultRequestTimeoutMs);
    }

    private static async Task<TResponse> ReadAndCheckAsync<TResponse>(HttpResponseMessage response, ConnectorContext context, CancellationToken cancellationToken)
        where TResponse : WeComApiResponse, new()
    {
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new ConnectorException(ConnectorErrorCodes.Unknown, "WeCom returned empty body.", WeComConnectorMarker.ProviderType);

        if (body.ErrCode != 0)
        {
            var code = WeComErrorMapper.MapErrorCode(body.ErrCode);
            throw new ConnectorException(code, body.ErrMsg ?? "WeCom api error", WeComConnectorMarker.ProviderType, body.ErrCode, body.ErrMsg);
        }
        return body;
    }

    private static string BuildAccessTokenCacheKey(Guid tenantId, long providerInstanceId, string credentialFingerprint)
        => $"connector:wecom:{tenantId:D}:{providerInstanceId}:{credentialFingerprint}:access_token";
}

internal sealed record WeComCachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

internal static class WeComErrorMapper
{
    public static string MapErrorCode(int errcode) => errcode switch
    {
        0 => "ok",
        40029 => ConnectorErrorCodes.OAuthCodeInvalid,
        50001 or 50002 or 50003 => ConnectorErrorCodes.TrustedDomainMismatch,
        // 企微「应用可见范围不足」族：60011/60020/60101/60102/60103/60123/60124 等，统一降级。
        60011 or 60020 or 60101 or 60102 or 60103 or 60104 or 60111 or 60121 or 60122 or 60123 or 60124
            => ConnectorErrorCodes.VisibilityScopeDenied,
        42001 or 40014 or 41001 => ConnectorErrorCodes.TokenExpired,
        40013 => ConnectorErrorCodes.TokenInvalid,
        301002 or 301025 or 301026 or 301027 => ConnectorErrorCodes.ApprovalSubmitFailed,
        _ => ConnectorErrorCodes.Unknown,
    };
}
