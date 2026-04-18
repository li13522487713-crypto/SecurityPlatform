using Atlas.Connectors.Core.Models;

namespace Atlas.Connectors.Core.Abstractions;

/// <summary>
/// 外部身份 Provider：负责 OAuth 登录与身份解析。
/// 不依赖任何本地账户模型，只返回 ExternalUserProfile。账号绑定由 Application 层完成。
/// </summary>
public interface IExternalIdentityProvider
{
    string ProviderType { get; }

    /// <summary>
    /// 构造跳转到外部 OAuth 授权端的 URL。state 由调用方生成并塞入 OAuthStateStore。
    /// </summary>
    Uri BuildAuthorizationUrl(ConnectorContext context, string redirectUri, string state, IReadOnlyList<string>? scopes, CancellationToken cancellationToken);

    /// <summary>
    /// 用 OAuth code 换 access token + 解析身份（返回核心字段，不补拉详细资料）。
    /// 实现应在内部完成可信域名匹配（企微 50001 错误必须前置拦截）。
    /// </summary>
    Task<ExternalUserProfile> ExchangeCodeAsync(ConnectorContext context, string code, string redirectUri, CancellationToken cancellationToken);

    /// <summary>
    /// 通过 ExternalUserId 拉取详细档案（部门、邮箱、手机号、头像等）。
    /// 通常在登录后异步调用以补充本地 ExternalUserMirror。
    /// </summary>
    Task<ExternalUserProfile> GetUserProfileAsync(ConnectorContext context, string externalUserId, CancellationToken cancellationToken);

    /// <summary>
    /// 在 userid / open id / open_userid 之间互转。
    /// 对不支持互转的 provider（如飞书自带三种 ID 同返回），可直接返回原值。
    /// </summary>
    Task<string?> ConvertIdAsync(ConnectorContext context, string sourceId, ExternalIdConversion conversion, CancellationToken cancellationToken);
}

public enum ExternalIdConversion
{
    UserIdToOpenId = 1,
    OpenIdToUserId = 2,
    UserIdToOpenUserId = 3,
    OpenUserIdToUserId = 4,
}
