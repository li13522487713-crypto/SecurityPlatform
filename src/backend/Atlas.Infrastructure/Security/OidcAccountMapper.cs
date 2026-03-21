using Atlas.Application.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Infrastructure.Security;

/// <summary>
/// OIDC 账号映射服务：将外部 OIDC 用户映射到平台本地账号，并记录 OidcLink 绑定关系。
/// 支持多 IdP，查找优先级：providerId+sub → providerId+email → 创建新账号并建立绑定。
/// </summary>
public sealed class OidcAccountMapper
{
    private readonly IUserAccountRepository _userRepo;
    private readonly OidcLinkRepository _oidcLinkRepo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ILogger<OidcAccountMapper> _logger;

    public OidcAccountMapper(
        IUserAccountRepository userRepo,
        OidcLinkRepository oidcLinkRepo,
        IIdGeneratorAccessor idGen,
        ILogger<OidcAccountMapper> logger)
    {
        _userRepo = userRepo;
        _oidcLinkRepo = oidcLinkRepo;
        _idGen = idGen;
        _logger = logger;
    }

    /// <summary>
    /// 从 OIDC ID Token claims 映射/创建本地账号，返回本地 UserAccount。
    /// </summary>
    /// <param name="principal">OIDC claims principal</param>
    /// <param name="tenantId">目标租户</param>
    /// <param name="providerId">IdP 标识（如 "github"、"default"）</param>
    /// <param name="cancellationToken"></param>
    public async Task<UserAccount> MapOrCreateAsync(
        ClaimsPrincipal principal,
        TenantId tenantId,
        string providerId,
        CancellationToken cancellationToken)
    {
        var sub = GetClaimValue(principal, JwtRegisteredClaimNames.Sub)
            ?? GetClaimValue(principal, ClaimTypes.NameIdentifier)
            ?? string.Empty;

        var email = GetClaimValue(principal, JwtRegisteredClaimNames.Email)
            ?? GetClaimValue(principal, ClaimTypes.Email)
            ?? string.Empty;

        var displayName = GetClaimValue(principal, "name")
            ?? GetClaimValue(principal, ClaimTypes.Name)
            ?? email
            ?? sub;

        // 1. 优先按 providerId + sub 查找已有绑定
        if (!string.IsNullOrEmpty(sub))
        {
            var link = await _oidcLinkRepo.FindByProviderSubAsync(tenantId, providerId, sub, cancellationToken);
            if (link is not null)
            {
                var existing = await _userRepo.FindByIdAsync(tenantId, link.UserId, cancellationToken);
                if (existing is not null)
                {
                    link.RecordLogin();
                    await _oidcLinkRepo.UpdateAsync(link, cancellationToken);
                    _logger.LogInformation("OIDC [{Provider}] user {Sub} matched via OidcLink to account {Id}", providerId, sub, existing.Id);
                    return existing;
                }
            }
        }

        // 2. 按 email 查找现有账号（跨 IdP 自动关联）
        if (!string.IsNullOrEmpty(email))
        {
            var existingByEmail = await _userRepo.FindByEmailAsync(tenantId, email, cancellationToken);
            if (existingByEmail is not null)
            {
                // 为该账号创建 OidcLink 绑定（自动关联）
                if (!string.IsNullOrEmpty(sub))
                {
                    var newLink = new OidcLink(tenantId, existingByEmail.Id, providerId, sub, email, _idGen.NextId());
                    newLink.RecordLogin();
                    await _oidcLinkRepo.AddAsync(newLink, cancellationToken);
                }
                _logger.LogInformation("OIDC [{Provider}] user {Sub} auto-linked to existing account {Id} by email", providerId, sub, existingByEmail.Id);
                return existingByEmail;
            }
        }

        // 3. 向后兼容：按旧 oidc_{hash} 用户名查找
        if (!string.IsNullOrEmpty(sub))
        {
            var legacyUsername = BuildOidcUsername(sub);
            var legacyAccount = await _userRepo.FindByUsernameAsync(tenantId, legacyUsername, cancellationToken);
            if (legacyAccount is not null)
            {
                // 补建 OidcLink
                var migrateLink = new OidcLink(tenantId, legacyAccount.Id, providerId, sub, email, _idGen.NextId());
                migrateLink.RecordLogin();
                await _oidcLinkRepo.AddAsync(migrateLink, cancellationToken);
                _logger.LogInformation("OIDC [{Provider}] user {Sub} migrated legacy account {Id} to OidcLink", providerId, sub, legacyAccount.Id);
                return legacyAccount;
            }
        }

        // 4. 创建新账号（空密码哈希，仅 OIDC 登录）
        var userId = _idGen.Generator.NextId();
        var oidcUsername = $"sso_{providerId}_{BuildShortHash(sub)}";
        var account = new UserAccount(tenantId, oidcUsername, displayName, string.Empty, userId);
        account.UpdateProfile(displayName, email, null);
        await _userRepo.AddAsync(account, cancellationToken);

        if (!string.IsNullOrEmpty(sub))
        {
            var createLink = new OidcLink(tenantId, userId, providerId, sub, email, _idGen.NextId());
            createLink.RecordLogin();
            await _oidcLinkRepo.AddAsync(createLink, cancellationToken);
        }

        _logger.LogInformation("OIDC [{Provider}] user {Sub} created new account {Id}", providerId, sub, userId);
        return account;
    }

    private static string BuildOidcUsername(string sub)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sub));
        var hash = Convert.ToHexString(bytes)[..16].ToLowerInvariant();
        return $"oidc_{hash}";
    }

    private static string BuildShortHash(string input)
    {
        if (string.IsNullOrEmpty(input)) return "anon";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    private static string? GetClaimValue(ClaimsPrincipal principal, string claimType)
    {
        return principal.FindFirst(claimType)?.Value;
    }
}
