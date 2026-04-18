using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.DingTalk.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Connectors.DingTalk;

/// <summary>
/// 钉钉身份 Provider：
/// - OAuth：login.dingtalk.com/oauth2/auth?redirect_uri + clientId；
/// - code → userAccessToken：v1.0 /v1.0/oauth2/userAccessToken；
/// - userid/unionid 互转：v1 /topapi/user/getbyunionid；
/// - 详情：v1 /topapi/v2/user/get。
/// </summary>
public sealed class DingTalkIdentityProvider : IExternalIdentityProvider
{
    private readonly DingTalkApiClient _api;
    private readonly DingTalkOptions _options;
    private readonly ILogger<DingTalkIdentityProvider> _logger;

    public DingTalkIdentityProvider(
        DingTalkApiClient api,
        IOptions<DingTalkOptions> options,
        ILogger<DingTalkIdentityProvider> logger)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderType => DingTalkConnectorMarker.ProviderType;

    public Uri BuildAuthorizationUrl(ConnectorContext context, string redirectUri, string state, IReadOnlyList<string>? scopes, CancellationToken cancellationToken)
    {
        var runtime = _api.ResolveRuntimeOptionsAsync(context, cancellationToken).GetAwaiter().GetResult();
        ValidateRedirectAgainstTrustedDomains(runtime, redirectUri);
        var scope = scopes is { Count: > 0 } ? string.Join(' ', scopes) : "openid";

        // 钉钉 OAuth2：https://login.dingtalk.com/oauth2/auth?redirect_uri=...&response_type=code&client_id=...&scope=openid&state=...&prompt=consent
        var url = string.Concat(
            _options.OAuthBaseUrl,
            "/oauth2/auth",
            "?redirect_uri=", Uri.EscapeDataString(redirectUri),
            "&response_type=code",
            "&client_id=", Uri.EscapeDataString(runtime.AppKey),
            "&scope=", Uri.EscapeDataString(scope),
            "&state=", Uri.EscapeDataString(state),
            "&prompt=consent");
        return new Uri(url);
    }

    public async Task<ExternalUserProfile> ExchangeCodeAsync(ConnectorContext context, string code, string redirectUri, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ConnectorException(ConnectorErrorCodes.OAuthCodeInvalid, "DingTalk OAuth code missing.", ProviderType);
        }

        var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);
        ValidateRedirectAgainstTrustedDomains(runtime, redirectUri);

        var userToken = await _api.ExchangeUserAccessTokenAsync(context, code, cancellationToken).ConfigureAwait(false);

        // v1.0 /contact/users/me GET 携带 user-level Bearer 换取用户身份（unionId + openid）
        var contactMe = await _api.SendV1UserGetAsync<DingTalkContactMeResponse>(userToken.AccessToken!, "/v1.0/contact/users/me", cancellationToken).ConfigureAwait(false);

        // 根据 unionId 换 userid：v1 /topapi/user/getbyunionid （企业通讯录内的用户才有 userid）
        string? userId = null;
        if (!string.IsNullOrEmpty(contactMe.UnionId))
        {
            userId = await ResolveUserIdByUnionIdAsync(context, contactMe.UnionId, cancellationToken).ConfigureAwait(false);
        }

        return new ExternalUserProfile
        {
            ProviderType = ProviderType,
            ProviderTenantId = userToken.CorpId ?? runtime.CorpId ?? runtime.AppKey,
            ExternalUserId = userId ?? contactMe.UnionId ?? contactMe.OpenId ?? contactMe.Nick ?? string.Empty,
            OpenId = contactMe.OpenId,
            UnionId = contactMe.UnionId,
            Name = contactMe.Nick,
            Email = contactMe.Email,
            Mobile = contactMe.Mobile,
            Avatar = contactMe.AvatarUrl,
            RawJson = JsonSerializer.Serialize(new { me = contactMe, userId }),
        };
    }

    public async Task<ExternalUserProfile> GetUserProfileAsync(ConnectorContext context, string externalUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            throw new ConnectorException(ConnectorErrorCodes.IdentityNotFound, "DingTalk user/get requires non-empty userid.", ProviderType);
        }

        var body = new { userid = externalUserId, language = "zh_CN" };
        var resp = await _api.SendLegacyPostJsonAsync<object, DingTalkLegacyUserDetailResponse>(context, "/topapi/v2/user/get", body, cancellationToken).ConfigureAwait(false);
        var runtime = await _api.ResolveRuntimeOptionsAsync(context, cancellationToken).ConfigureAwait(false);

        return MapUser(runtime.CorpId ?? runtime.AppKey, resp.Result) with
        {
            RawJson = JsonSerializer.Serialize(resp),
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
            case ExternalIdConversion.OpenIdToUserId:
            case ExternalIdConversion.OpenUserIdToUserId:
                // 钉钉 openId / unionId → userid 走 /topapi/user/getbyunionid
                return await ResolveUserIdByUnionIdAsync(context, sourceId, cancellationToken).ConfigureAwait(false);

            case ExternalIdConversion.UserIdToOpenId:
            case ExternalIdConversion.UserIdToOpenUserId:
                {
                    var detail = await GetUserProfileAsync(context, sourceId, cancellationToken).ConfigureAwait(false);
                    return conversion == ExternalIdConversion.UserIdToOpenId ? detail.OpenId ?? detail.UnionId : detail.UnionId;
                }
            default:
                return null;
        }
    }

    private async Task<string?> ResolveUserIdByUnionIdAsync(ConnectorContext context, string unionId, CancellationToken cancellationToken)
    {
        try
        {
            var body = new { unionid = unionId };
            var resp = await _api.SendLegacyPostJsonAsync<object, DingTalkLegacyGetUserIdByMobileResponse>(context, "/topapi/user/getbyunionid", body, cancellationToken).ConfigureAwait(false);
            return resp.Result?.UserId;
        }
        catch (ConnectorException ex) when (string.Equals(ex.Code, ConnectorErrorCodes.IdentityNotFound, StringComparison.Ordinal))
        {
            return null;
        }
    }

    private static ExternalUserProfile MapUser(string providerTenantId, DingTalkUserDetail? user)
    {
        if (user is null)
        {
            return new ExternalUserProfile
            {
                ProviderType = DingTalkConnectorMarker.ProviderType,
                ProviderTenantId = providerTenantId,
                ExternalUserId = string.Empty,
            };
        }
        return new ExternalUserProfile
        {
            ProviderType = DingTalkConnectorMarker.ProviderType,
            ProviderTenantId = providerTenantId,
            ExternalUserId = user.UserId ?? string.Empty,
            OpenId = null,
            UnionId = user.UnionId,
            Name = user.Name,
            Email = user.OrgEmail ?? user.Email,
            Mobile = user.Mobile,
            Avatar = user.Avatar,
            Position = user.Title,
            DepartmentIds = user.DeptIdList?.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
            PrimaryDepartmentId = user.DeptIdList is { Length: > 0 }
                ? user.DeptIdList[0].ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null,
            Status = user.Active ? "active" : "inactive",
        };
    }

    private void ValidateRedirectAgainstTrustedDomains(DingTalkRuntimeOptions runtime, string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new ConnectorException(ConnectorErrorCodes.TrustedDomainMismatch, "DingTalk redirect uri is empty.", ProviderType);
        }
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
        {
            throw new ConnectorException(ConnectorErrorCodes.TrustedDomainMismatch, $"DingTalk redirect uri '{redirectUri}' is not absolute.", ProviderType);
        }
        if (runtime.TrustedDomains.Count == 0)
        {
            return;
        }
        foreach (var d in runtime.TrustedDomains)
        {
            if (string.Equals(d, uri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        throw new ConnectorException(ConnectorErrorCodes.TrustedDomainMismatch, $"DingTalk redirect host '{uri.Host}' not in trusted domains of provider {runtime.AppKey}.", ProviderType);
    }
}

internal sealed class DingTalkContactMeResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("nick")]
    public string? Nick { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("mobile")]
    public string? Mobile { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("openId")]
    public string? OpenId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("unionId")]
    public string? UnionId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("email")]
    public string? Email { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("stateCode")]
    public string? StateCode { get; set; }
}
