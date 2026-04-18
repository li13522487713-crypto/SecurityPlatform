using System.Collections.Generic;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.WeCom.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Connectors.WeCom;

/// <summary>
/// 企业微信身份 Provider：
/// - OAuth: code → userid（auth/getuserinfo）；
/// - 详情：user/get；
/// - 互转：user/convert_to_openid + user/convert_to_userid；
/// - 可信域名校验在 BuildAuthorizationUrl 时前置拦截，避免 50001。
/// </summary>
public sealed class WeComIdentityProvider : IExternalIdentityProvider
{
    private readonly WeComApiClient _api;
    private readonly WeComOptions _options;
    private readonly ILogger<WeComIdentityProvider> _logger;

    public WeComIdentityProvider(
        WeComApiClient api,
        IOptions<WeComOptions> options,
        ILogger<WeComIdentityProvider> logger)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderType => WeComConnectorMarker.ProviderType;

    public Uri BuildAuthorizationUrl(ConnectorContext context, string redirectUri, string state, IReadOnlyList<string>? scopes, CancellationToken cancellationToken)
    {
        ValidateRedirectAgainstTrustedDomains(context, redirectUri).GetAwaiter().GetResult();
        var runtime = _api.ResolveRuntimeOptionsAsync(context, cancellationToken).GetAwaiter().GetResult();
        var scope = scopes is { Count: > 0 } ? scopes[0] : _options.OAuthScope;

        // 企微 OAuth2 跳转：https://open.weixin.qq.com/connect/oauth2/authorize?appid=CORPID&redirect_uri=...&response_type=code&scope=snsapi_base&agentid=AGENTID&state=STATE#wechat_redirect
        var url = string.Concat(
            _options.OAuthBaseUrl,
            "/connect/oauth2/authorize",
            "?appid=", Uri.EscapeDataString(runtime.CorpId),
            "&redirect_uri=", Uri.EscapeDataString(redirectUri),
            "&response_type=code",
            "&scope=", Uri.EscapeDataString(scope),
            "&agentid=", Uri.EscapeDataString(runtime.AgentId),
            "&state=", Uri.EscapeDataString(state),
            "#wechat_redirect");
        return new Uri(url);
    }

    public async Task<ExternalUserProfile> ExchangeCodeAsync(ConnectorContext context, string code, string redirectUri, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ConnectorException(ConnectorErrorCodes.OAuthCodeInvalid, "WeCom OAuth code missing.", ProviderType);
        }

        await ValidateRedirectAgainstTrustedDomains(context, redirectUri).ConfigureAwait(false);

        var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["code"] = code };
        var response = await _api.SendAuthorizedGetAsync<WeComGetUserInfoResponse>(context, "/cgi-bin/auth/getuserinfo", query, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(response.UserId) && string.IsNullOrEmpty(response.OpenId))
        {
            throw new ConnectorException(ConnectorErrorCodes.IdentityNotFound, "WeCom getuserinfo returned neither userid nor openid.", ProviderType);
        }

        var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);
        var rawJson = JsonSerializer.Serialize(response);

        return new ExternalUserProfile
        {
            ProviderType = ProviderType,
            ProviderTenantId = runtime.CorpId,
            ExternalUserId = response.UserId ?? response.OpenId!,
            OpenId = response.OpenId,
            UnionId = response.ExternalUserId,
            RawJson = rawJson,
        };
    }

    public async Task<ExternalUserProfile> GetUserProfileAsync(ConnectorContext context, string externalUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            throw new ConnectorException(ConnectorErrorCodes.IdentityNotFound, "WeCom user/get requires non-empty userid.", ProviderType);
        }

        var query = new Dictionary<string, string>(StringComparer.Ordinal) { ["userid"] = externalUserId };
        var detail = await _api.SendAuthorizedGetAsync<WeComUserDetailResponse>(context, "/cgi-bin/user/get", query, cancellationToken).ConfigureAwait(false);
        var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);

        return new ExternalUserProfile
        {
            ProviderType = ProviderType,
            ProviderTenantId = runtime.CorpId,
            ExternalUserId = detail.UserId ?? externalUserId,
            OpenId = null,
            UnionId = detail.OpenUserId,
            Name = detail.Name,
            EnglishName = detail.EnglishName,
            Email = detail.Email ?? detail.BizMail,
            Mobile = detail.Mobile,
            Avatar = detail.Avatar,
            Position = detail.Position,
            DepartmentIds = detail.Departments?.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
            PrimaryDepartmentId = detail.MainDepartment > 0 ? detail.MainDepartment.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
            Status = detail.Status.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RawJson = JsonSerializer.Serialize(detail),
        };
    }

    public async Task<string?> ConvertIdAsync(ConnectorContext context, string sourceId, ExternalIdConversion conversion, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return null;
        }

        switch (conversion)
        {
            case ExternalIdConversion.UserIdToOpenId:
            {
                var body = new { userid = sourceId };
                var resp = await _api.SendAuthorizedPostJsonAsync<object, WeComConvertOpenIdResponse>(context, "/cgi-bin/user/convert_to_openid", body, null, cancellationToken).ConfigureAwait(false);
                return resp.OpenId;
            }
            case ExternalIdConversion.OpenIdToUserId:
            {
                var body = new { openid = sourceId };
                var resp = await _api.SendAuthorizedPostJsonAsync<object, WeComConvertUserIdResponse>(context, "/cgi-bin/user/convert_to_userid", body, null, cancellationToken).ConfigureAwait(false);
                return resp.UserId;
            }
            case ExternalIdConversion.UserIdToOpenUserId:
            {
                var body = new { userid_list = new[] { sourceId } };
                var raw = await _api.SendAuthorizedPostJsonAsync<object, WeComBatchConvertResponse>(context, "/cgi-bin/batch/userid_to_openuserid", body, null, cancellationToken).ConfigureAwait(false);
                return raw.OpenUserIdList?.FirstOrDefault()?.OpenUserId;
            }
            case ExternalIdConversion.OpenUserIdToUserId:
            {
                _logger.LogInformation("WeCom does not provide a direct openuserid → userid api; caller should query ExternalIdentityBinding mirror first.");
                return null;
            }
            default:
                return null;
        }
    }

    private async Task ValidateRedirectAgainstTrustedDomains(ConnectorContext context, string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new ConnectorException(ConnectorErrorCodes.TrustedDomainMismatch, "WeCom redirect uri is empty.", ProviderType);
        }

        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
        {
            throw new ConnectorException(ConnectorErrorCodes.TrustedDomainMismatch, $"WeCom redirect uri '{redirectUri}' is not an absolute URI.", ProviderType);
        }

        var runtime = await _api.ResolveRuntimeOptionsAsync(context, CancellationToken.None).ConfigureAwait(false);
        if (runtime.TrustedDomains.Count == 0)
        {
            // 显式未配置可信域名时，按企微生产建议拒绝；开发环境应通过 ExternalConnectors 配置补齐。
            throw new ConnectorException(ConnectorErrorCodes.TrustedDomainMismatch, "WeCom provider has no trusted domains configured. Enterprise WeChat will return 50001 if redirect host mismatches the agent trusted domain list.", ProviderType);
        }

        foreach (var domain in runtime.TrustedDomains)
        {
            if (string.Equals(domain, uri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        throw new ConnectorException(
            ConnectorErrorCodes.TrustedDomainMismatch,
            $"WeCom redirect host '{uri.Host}' is not in the trusted domain list of provider {context.ProviderInstanceId}.",
            ProviderType);
    }
}
