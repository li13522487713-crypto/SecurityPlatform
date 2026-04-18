using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Atlas.Application.Abstractions;
using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Options;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Atlas.PlatformHost.ExternalConnectors.Bridges;

/// <summary>
/// 外部连接器登录成功后的 JWT 签发桥接：复用现有 JwtOptions / RBAC / 会话仓储，
/// 但跳过密码校验链路（绑定关系本身已经证明用户身份合法）。
/// </summary>
public sealed class ConnectorJwtIssuerBridge : IConnectorJwtIssuer
{
    private readonly IOptionsMonitor<JwtOptions> _jwtOptions;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAuthSessionRepository _authSessionRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IRbacResolver _rbacResolver;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly TimeProvider _timeProvider;

    public ConnectorJwtIssuerBridge(
        IOptionsMonitor<JwtOptions> jwtOptions,
        IUserAccountRepository userAccountRepository,
        IAuthSessionRepository authSessionRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITenantProvider tenantProvider,
        IRbacResolver rbacResolver,
        IIdGeneratorAccessor idGeneratorAccessor,
        TimeProvider timeProvider)
    {
        _jwtOptions = jwtOptions;
        _userAccountRepository = userAccountRepository;
        _authSessionRepository = authSessionRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tenantProvider = tenantProvider;
        _rbacResolver = rbacResolver;
        _idGeneratorAccessor = idGeneratorAccessor;
        _timeProvider = timeProvider;
    }

    public async Task<ConnectorJwtIssueResult> IssueAsync(long localUserId, string sourceProvider, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var account = await _userAccountRepository.FindByIdAsync(tenantId, localUserId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessException("CONNECTOR_LOCAL_USER_NOT_FOUND", $"Local user {localUserId} not found.");

        if (!account.IsActive)
        {
            throw new BusinessException("CONNECTOR_LOCAL_USER_DISABLED", $"Local user {localUserId} disabled.");
        }

        var jwtOptions = _jwtOptions.CurrentValue;
        var now = _timeProvider.GetUtcNow();

        // 1. 创建会话与 refresh token，便于后续刷新和撤销
        var sessionId = _idGeneratorAccessor.NextId();
        var sessionExpires = now.AddMinutes(jwtOptions.SessionExpiresMinutes);
        var session = new AuthSession(
            tenantId,
            account.Id,
            clientType: "web",
            clientPlatform: "web",
            clientChannel: $"connector:{sourceProvider}",
            clientAgent: "connector-oauth",
            ipAddress: null,
            userAgent: null,
            now,
            sessionExpires,
            sessionId);
        await _authSessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);

        var refreshExpires = now.AddMinutes(jwtOptions.RefreshExpiresMinutes);
        var refreshTokenBytes = RandomNumberGenerator.GetBytes(64);
        var refreshToken = Base64UrlEncoder.Encode(refreshTokenBytes);
        var refreshTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        var refreshEntity = new RefreshToken(tenantId, account.Id, sessionId, refreshTokenHash, now, refreshExpires, _idGeneratorAccessor.NextId());
        await _refreshTokenRepository.AddAsync(refreshEntity, cancellationToken).ConfigureAwait(false);

        // 2. 签发 access token
        var roleCodes = await _rbacResolver.GetRoleCodesAsync(account, tenantId, cancellationToken).ConfigureAwait(false);
        var accessExpires = now.AddMinutes(jwtOptions.ExpiresMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Username),
            new("tenant_id", tenantId.Value.ToString("D")),
            new("sid", sessionId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, account.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, account.Username),
            new("display_name", account.DisplayName),
            new("is_platform_admin", account.IsPlatformAdmin ? "true" : "false"),
            new("auth_source", $"connector:{sourceProvider}"),
        };
        if (roleCodes.Count > 0)
        {
            claims.AddRange(roleCodes.Select(role => new Claim(ClaimTypes.Role, role)));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            jwtOptions.Issuer,
            jwtOptions.Audience,
            claims,
            notBefore: now.UtcDateTime,
            expires: accessExpires.UtcDateTime,
            signingCredentials: credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        return new ConnectorJwtIssueResult
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = accessExpires,
        };
    }
}
