using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Caching;
using Atlas.Connectors.WeCom.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Connectors.WeCom;

/// <summary>
/// 企微 API 客户端。负责：
/// 1. 通过 IHttpClientFactory 命名 wecom-api 拿 HttpClient；
/// 2. 通过 IConnectorTokenCache 缓存 access_token；
/// 3. 提供身份 / 通讯录 / 审批 / 消息相关的低层 GET/POST 方法（具体业务封装由 4 个 Provider 调用）。
/// </summary>
public sealed class WeComApiClient
{
    public const string HttpClientName = "wecom-api";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConnectorTokenCache _tokenCache;
    private readonly IConnectorRuntimeOptionsResolver<WeComRuntimeOptions> _runtimeOptionsResolver;
    private readonly WeComOptions _options;
    private readonly ILogger<WeComApiClient> _logger;

    public WeComApiClient(
        IHttpClientFactory httpClientFactory,
        IConnectorTokenCache tokenCache,
        IConnectorRuntimeOptionsResolver<WeComRuntimeOptions> runtimeOptionsResolver,
        IOptions<WeComOptions> options,
        ILogger<WeComApiClient> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _tokenCache = tokenCache ?? throw new ArgumentNullException(nameof(tokenCache));
        _runtimeOptionsResolver = runtimeOptionsResolver ?? throw new ArgumentNullException(nameof(runtimeOptionsResolver));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<WeComRuntimeOptions> ResolveRuntimeOptionsAsync(ConnectorContext context, CancellationToken cancellationToken)
        => _runtimeOptionsResolver.ResolveAsync(context, cancellationToken);

    /// <summary>
    /// 拿到当前 provider 实例的 access_token。同一进程的并发请求会通过 IConnectorTokenCache 串行刷新。
    /// </summary>
    public async Task<string> GetAccessTokenAsync(ConnectorContext context, CancellationToken cancellationToken)
    {
        var runtime = await _runtimeOptionsResolver.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var cacheKey = BuildAccessTokenCacheKey(context.TenantId, context.ProviderInstanceId);

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
        var cacheKey = BuildAccessTokenCacheKey(context.TenantId, context.ProviderInstanceId);
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

    private static string BuildAccessTokenCacheKey(Guid tenantId, long providerInstanceId)
        => $"connector:wecom:{tenantId:D}:{providerInstanceId}:access_token";
}

internal sealed record WeComCachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);

internal static class WeComErrorMapper
{
    public static string MapErrorCode(int errcode) => errcode switch
    {
        0 => "ok",
        40029 => ConnectorErrorCodes.OAuthCodeInvalid,
        50001 => ConnectorErrorCodes.TrustedDomainMismatch,
        60011 => ConnectorErrorCodes.VisibilityScopeDenied,
        42001 or 40014 => ConnectorErrorCodes.TokenExpired,
        301002 or 301025 or 301026 => ConnectorErrorCodes.ApprovalSubmitFailed,
        _ => ConnectorErrorCodes.Unknown,
    };
}
