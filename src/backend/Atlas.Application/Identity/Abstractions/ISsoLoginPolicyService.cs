using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

/// <summary>
/// 治理 M-G07-C5（S14）：SSO 登录策略服务。
/// 负责处理 OIDC / SAML 登录后首次进入系统的初始化：
/// 1. 自动加入默认组织；
/// 2. 应用 IdP 配置中映射的 RoleCode；
/// 3. 写审计 SSO_LOGIN。
/// </summary>
public interface ISsoLoginPolicyService
{
    /// <summary>首次登录或后续登录均触发：幂等加入默认组织。</summary>
    Task ApplyAsync(
        TenantId tenantId,
        long userId,
        string? roleCode,
        string idpType,
        string idpCode,
        CancellationToken cancellationToken);
}
