using System.Globalization;
using System.Text.Json;
using Atlas.Connectors.Core;
using Atlas.Connectors.Core.Abstractions;
using Atlas.Connectors.Core.Models;
using Atlas.Connectors.Feishu.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Atlas.Connectors.Feishu;

/// <summary>
/// 飞书身份 Provider：
/// - OAuth: authorization_code → user_access_token；
/// - 解析 open_id / user_id / union_id / email / mobile / tenant_key；
/// - 详情：contact/v3/users/{user_id}；
/// - 互转：contact API 默认返回三种 ID，因此 ConvertId 实现退化为查 contact 详情。
/// </summary>
public sealed class FeishuIdentityProvider : IExternalIdentityProvider
{
    private readonly FeishuApiClient _api;
    private readonly FeishuOptions _options;
    private readonly ILogger<FeishuIdentityProvider> _logger;

    public FeishuIdentityProvider(
        FeishuApiClient api,
        IOptions<FeishuOptions> options,
        ILogger<FeishuIdentityProvider> logger)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string ProviderType => FeishuConnectorMarker.ProviderType;

    public Uri BuildAuthorizationUrl(ConnectorContext context, string redirectUri, string state, IReadOnlyList<string>? scopes, CancellationToken cancellationToken)
    {
        var runtime = FeishuApiClient.ResolveRuntime(context);
        ValidateRedirectAgainstTrustedDomains(runtime, redirectUri);
        var scope = scopes is { Count: > 0 } ? string.Join(' ', scopes) : _options.OAuthScope;

        var url = string.Concat(
            _options.ApiBaseUrl,
            "/open-apis/authen/v1/authorize",
            "?app_id=", Uri.EscapeDataString(runtime.AppId),
            "&redirect_uri=", Uri.EscapeDataString(redirectUri),
            "&state=", Uri.EscapeDataString(state),
            string.IsNullOrEmpty(scope) ? string.Empty : $"&scope={Uri.EscapeDataString(scope)}");
        return new Uri(url);
    }

    public async Task<ExternalUserProfile> ExchangeCodeAsync(ConnectorContext context, string code, string redirectUri, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ConnectorException(ConnectorErrorCodes.OAuthCodeInvalid, "Feishu OAuth code missing.", ProviderType);
        }

        var runtime = FeishuApiClient.ResolveRuntime(context);
        ValidateRedirectAgainstTrustedDomains(runtime, redirectUri);

        var token = await _api.ExchangeUserAccessTokenAsync(context, code, redirectUri, cancellationToken).ConfigureAwait(false);
        var info = await _api.GetUserInfoAsync(token.AccessToken!, cancellationToken).ConfigureAwait(false);

        var external = info.UserId ?? info.OpenId ?? info.UnionId
            ?? throw new ConnectorException(ConnectorErrorCodes.IdentityNotFound, "Feishu authen returned neither user_id nor open_id.", ProviderType);

        // 命中身份后立即写 user_access_token 缓存，后续 provider 调用可通过
        // FeishuApiClient.GetCachedUserAccessTokenAsync(context, externalUserId) 复用，省掉重复 OAuth。
        await _api.CacheUserAccessTokenAsync(context, external, token, cancellationToken).ConfigureAwait(false);

        return new ExternalUserProfile
        {
            ProviderType = ProviderType,
            ProviderTenantId = info.TenantKey ?? runtime.TenantKey ?? runtime.AppId,
            ExternalUserId = external,
            OpenId = info.OpenId,
            UnionId = info.UnionId,
            Name = info.Name,
            EnglishName = info.EnName,
            Email = info.EnterpriseEmail ?? info.Email,
            Mobile = info.Mobile,
            Avatar = info.AvatarUrl,
            RawJson = JsonSerializer.Serialize(info),
        };
    }

    public async Task<ExternalUserProfile> GetUserProfileAsync(ConnectorContext context, string externalUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            throw new ConnectorException(ConnectorErrorCodes.IdentityNotFound, "Feishu contact get requires non-empty external_user_id.", ProviderType);
        }

        var runtime = FeishuApiClient.ResolveRuntime(context);
        var idType = _options.DefaultUserIdType;
        var path = $"/open-apis/contact/v3/users/{Uri.EscapeDataString(externalUserId)}?user_id_type={idType}";
        var resp = await _api.SendTenantGetAsync<FeishuContactUserData>(context, path, cancellationToken).ConfigureAwait(false);
        var user = resp.Data?.User
            ?? throw new ConnectorException(ConnectorErrorCodes.IdentityNotFound, "Feishu contact returned empty user.", ProviderType);

        return new ExternalUserProfile
        {
            ProviderType = ProviderType,
            ProviderTenantId = runtime.TenantKey ?? runtime.AppId,
            ExternalUserId = user.UserId ?? user.OpenId ?? externalUserId,
            OpenId = user.OpenId,
            UnionId = user.UnionId,
            Name = user.Name ?? user.Nickname,
            EnglishName = user.EnName,
            Email = user.EnterpriseEmail ?? user.Email,
            Mobile = user.Mobile,
            Avatar = user.Avatar?.Avatar240 ?? user.Avatar?.Avatar72,
            Position = user.JobTitle,
            DepartmentIds = user.DepartmentIds,
            PrimaryDepartmentId = user.DepartmentIds is { Length: > 0 } ? user.DepartmentIds[0] : null,
            Status = user.Status is null
                ? null
                : string.Create(CultureInfo.InvariantCulture, $"activated={user.Status.IsActivated},resigned={user.Status.IsResigned},frozen={user.Status.IsFrozen}"),
            RawJson = JsonSerializer.Serialize(user),
        };
    }

    public async Task<string?> ConvertIdAsync(ConnectorContext context, string sourceId, ExternalIdConversion conversion, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return null;
        }

        // 飞书的 contact API 在返回 user 详情时同时给出 open_id / user_id / union_id，
        // 因此互转退化为一次 contact 查询；调用方传入的 sourceId 类型由请求 user_id_type 控制。
        var sourceIdType = conversion switch
        {
            ExternalIdConversion.UserIdToOpenId => "user_id",
            ExternalIdConversion.UserIdToOpenUserId => "user_id",
            ExternalIdConversion.OpenIdToUserId => "open_id",
            ExternalIdConversion.OpenUserIdToUserId => "union_id",
            _ => _options.DefaultUserIdType,
        };

        var path = $"/open-apis/contact/v3/users/{Uri.EscapeDataString(sourceId)}?user_id_type={sourceIdType}";
        try
        {
            var resp = await _api.SendTenantGetAsync<FeishuContactUserData>(context, path, cancellationToken).ConfigureAwait(false);
            var user = resp.Data?.User;
            if (user is null)
            {
                return null;
            }

            return conversion switch
            {
                ExternalIdConversion.UserIdToOpenId => user.OpenId,
                ExternalIdConversion.OpenIdToUserId => user.UserId,
                ExternalIdConversion.UserIdToOpenUserId => user.UnionId,
                ExternalIdConversion.OpenUserIdToUserId => user.UserId,
                _ => null,
            };
        }
        catch (ConnectorException ex)
        {
            _logger.LogInformation("Feishu ConvertId failed for source {SourceId}: {Code}", sourceId, ex.Code);
            return null;
        }
    }

    private void ValidateRedirectAgainstTrustedDomains(FeishuRuntimeOptions runtime, string redirectUri)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new ConnectorException(ConnectorErrorCodes.TrustedDomainMismatch, "Feishu redirect uri is empty.", ProviderType);
        }
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
        {
            throw new ConnectorException(ConnectorErrorCodes.TrustedDomainMismatch, $"Feishu redirect uri '{redirectUri}' is not absolute.", ProviderType);
        }
        if (runtime.TrustedDomains.Count == 0)
        {
            return; // 飞书未强制可信域名校验，默认放行；调用方可在 ExternalConnectors 层强制配置。
        }
        foreach (var d in runtime.TrustedDomains)
        {
            if (string.Equals(d, uri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        throw new ConnectorException(ConnectorErrorCodes.TrustedDomainMismatch, $"Feishu redirect host '{uri.Host}' not in trusted domains of provider {runtime.AppId}.", ProviderType);
    }
}
