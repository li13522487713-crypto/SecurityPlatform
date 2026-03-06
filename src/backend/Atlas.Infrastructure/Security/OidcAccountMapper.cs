using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Atlas.Application.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Security;

/// <summary>
/// OIDC 账号映射服务：将外部 OIDC 用户映射到平台本地账号
/// </summary>
public sealed class OidcAccountMapper
{
    private readonly IUserAccountRepository _userRepo;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly ILogger<OidcAccountMapper> _logger;

    public OidcAccountMapper(
        IUserAccountRepository userRepo,
        IIdGeneratorAccessor idGen,
        ILogger<OidcAccountMapper> logger)
    {
        _userRepo = userRepo;
        _idGen = idGen;
        _logger = logger;
    }

    /// <summary>
    /// 从 OIDC ID Token claims 映射/创建本地账号，返回本地 UserAccount。
    /// 映射策略：优先按 email 匹配现有账号，其次按 oidc:sub 前缀匹配，找不到则自动创建。
    /// </summary>
    public async Task<UserAccount> MapOrCreateAsync(
        ClaimsPrincipal principal,
        TenantId tenantId,
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

        // 尝试按 oidc_sub 前缀的用户名查找
        var oidcUsername = BuildOidcUsername(sub);
        var existing = await _userRepo.FindByUsernameAsync(tenantId, oidcUsername, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        // 尝试按 email 查找
        if (!string.IsNullOrEmpty(email))
        {
            var existingByEmail = await _userRepo.FindByEmailAsync(tenantId, email, cancellationToken);
            if (existingByEmail is not null)
            {
                _logger.LogInformation("OIDC user {Sub} matched existing account {Id} by email", sub, existingByEmail.Id);
                return existingByEmail;
            }
        }

        // 创建新账号（空密码哈希，仅 OIDC 登录）
        var userId = _idGen.Generator.NextId();
        var account = new UserAccount(tenantId, oidcUsername, displayName, string.Empty, userId);
        account.UpdateProfile(displayName, email, null);
        await _userRepo.AddAsync(account, cancellationToken);

        _logger.LogInformation("OIDC user {Sub} created new account {Id}", sub, userId);
        return account;
    }

    private static string BuildOidcUsername(string sub)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sub));
        var hash = Convert.ToHexString(bytes)[..16].ToLowerInvariant();
        return $"oidc_{hash}";
    }

    private static string? GetClaimValue(ClaimsPrincipal principal, string claimType)
    {
        return principal.FindFirst(claimType)?.Value;
    }
}
