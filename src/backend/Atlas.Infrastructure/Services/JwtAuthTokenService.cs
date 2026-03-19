using Atlas.Application.Abstractions;
using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Audit.Abstractions;
using Atlas.Application.Audit.Models;
using Atlas.Application.Models;
using Atlas.Application.Options;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Identity;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ITotpService = Atlas.Application.Abstractions.ITotpService;

namespace Atlas.Infrastructure.Services;

public sealed class JwtAuthTokenService : IAuthTokenService
{
    private readonly JwtOptions _jwtOptions;
    private readonly PasswordPolicyOptions _passwordPolicy;
    private readonly LockoutPolicyOptions _lockoutPolicy;
    private readonly SecurityOptions _securityOptions;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditRecorder _auditRecorder;
    private readonly IAuthSessionRepository _authSessionRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IAppContextAccessor _appContextAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly IRbacResolver _rbacResolver;
    private readonly ITotpService _totpService;
    private readonly ILoginLogWriteService _loginLogWriteService;
    private readonly IAuthCacheService _authCacheService;

    public JwtAuthTokenService(
        IOptions<JwtOptions> jwtOptions,
        IOptions<PasswordPolicyOptions> passwordPolicy,
        IOptions<LockoutPolicyOptions> lockoutPolicy,
        IOptions<SecurityOptions> securityOptions,
        IUserAccountRepository userAccountRepository,
        IPasswordHasher passwordHasher,
        IAuditRecorder auditRecorder,
        IAuthSessionRepository authSessionRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IIdGeneratorAccessor idGeneratorAccessor,
        IAppContextAccessor appContextAccessor,
        TimeProvider timeProvider,
        IRbacResolver rbacResolver,
        ITotpService totpService,
        ILoginLogWriteService loginLogWriteService,
        IAuthCacheService authCacheService)
    {
        _jwtOptions = jwtOptions.Value;
        _passwordPolicy = passwordPolicy.Value;
        _lockoutPolicy = lockoutPolicy.Value;
        _securityOptions = securityOptions.Value;
        _userAccountRepository = userAccountRepository;
        _passwordHasher = passwordHasher;
        _auditRecorder = auditRecorder;
        _authSessionRepository = authSessionRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
        _appContextAccessor = appContextAccessor;
        _timeProvider = timeProvider;
        _rbacResolver = rbacResolver;
        _totpService = totpService;
        _loginLogWriteService = loginLogWriteService;
        _authCacheService = authCacheService;
    }

    public async Task<AuthTokenResult> CreateTokenAsync(
        AuthTokenRequest request,
        TenantId tenantId,
        AuthRequestContext context,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var account = await _userAccountRepository.FindByUsernameAsync(tenantId, request.Username, cancellationToken);
        if (account is null)
        {
            await WriteAuditAsync(tenantId, request.Username, "LOGIN", "FAILED", null, context, cancellationToken);
            await WriteLoginLogAsync(tenantId, request.Username, context, false, "用户名或密码错误", now, cancellationToken);
            throw new BusinessException("用户名或密码错误", ErrorCodes.Unauthorized);
        }

        if (!account.IsActive)
        {
            await WriteAuditAsync(tenantId, request.Username, "LOGIN", "FAILED", null, context, cancellationToken);
            await WriteLoginLogAsync(tenantId, request.Username, context, false, "账号已停用", now, cancellationToken);
            throw new BusinessException("用户名或密码错误", ErrorCodes.Forbidden);
        }

        var locked = IsLocked(account, now, out var lockStateChanged);
        if (lockStateChanged)
        {
            await _userAccountRepository.UpdateAsync(account, cancellationToken);
        }

        if (locked)
        {
            await WriteAuditAsync(tenantId, request.Username, "LOGIN", "LOCKED", null, context, cancellationToken);
            await WriteLoginLogAsync(tenantId, request.Username, context, false, "账号已锁定", now, cancellationToken);
            throw new BusinessException("用户名或密码错误", ErrorCodes.AccountLocked);
        }

        var passwordExpiredAt = account.LastPasswordChangeAt.AddDays(_passwordPolicy.ExpirationDays);
        if (passwordExpiredAt <= now)
        {
            await WriteAuditAsync(tenantId, request.Username, "LOGIN", "PASSWORD_EXPIRED", null, context, cancellationToken);
            await WriteLoginLogAsync(tenantId, request.Username, context, false, "密码已过期", now, cancellationToken);
            throw new BusinessException("用户名或密码错误", ErrorCodes.PasswordExpired);
        }

        var passwordValid = _passwordHasher.VerifyHashedPassword(account.PasswordHash, request.Password);
        if (!passwordValid)
        {
            account.MarkLoginFailure(now, _lockoutPolicy.MaxFailedAttempts, TimeSpan.FromMinutes(_lockoutPolicy.LockoutMinutes));
            await _userAccountRepository.UpdateAsync(account, cancellationToken);
            await WriteAuditAsync(tenantId, request.Username, "LOGIN", "FAILED", null, context, cancellationToken);
            await WriteLoginLogAsync(tenantId, request.Username, context, false, "密码错误", now, cancellationToken);
            throw new BusinessException("用户名或密码错误", ErrorCodes.Unauthorized);
        }

        // MFA verification: if MFA is enabled for the user, require a valid TOTP code
        if (account.MfaEnabled && !string.IsNullOrWhiteSpace(account.MfaSecretKey))
        {
            if (string.IsNullOrWhiteSpace(request.TotpCode))
            {
                await WriteAuditAsync(tenantId, request.Username, "LOGIN", "MFA_REQUIRED", null, context, cancellationToken);
                await WriteLoginLogAsync(tenantId, request.Username, context, false, "需要多因素认证验证码", now, cancellationToken);
                throw new BusinessException("需要多因素认证验证码", ErrorCodes.MfaRequired);
            }

            if (!_totpService.ValidateCode(account.MfaSecretKey, request.TotpCode, now))
            {
                account.MarkLoginFailure(now, _lockoutPolicy.MaxFailedAttempts, TimeSpan.FromMinutes(_lockoutPolicy.LockoutMinutes));
                await _userAccountRepository.UpdateAsync(account, cancellationToken);
                await WriteAuditAsync(tenantId, request.Username, "LOGIN", "MFA_FAILED", null, context, cancellationToken);
                await WriteLoginLogAsync(tenantId, request.Username, context, false, "多因素认证验证码错误", now, cancellationToken);
                throw new BusinessException("用户名或密码错误", ErrorCodes.Unauthorized);
            }
        }

        account.MarkLoginSuccess(now);
        await _userAccountRepository.UpdateAsync(account, cancellationToken);
        await WriteAuditAsync(tenantId, request.Username, "LOGIN", "SUCCESS", null, context, cancellationToken);
        await WriteLoginLogAsync(tenantId, request.Username, context, true, null, now, cancellationToken);

        // Enforce concurrent session limit: revoke oldest sessions if at or over max
        if (_securityOptions.MaxConcurrentSessions > 0)
        {
            var activeCount = await _authSessionRepository.CountActiveByUserIdAsync(tenantId, account.Id, now, cancellationToken);
            if (activeCount >= _securityOptions.MaxConcurrentSessions)
            {
                var excessCount = activeCount - _securityOptions.MaxConcurrentSessions + 1;
                var oldestSessions = await _authSessionRepository.QueryOldestActiveByUserIdAsync(
                    tenantId, account.Id, now, excessCount, cancellationToken);
                foreach (var oldSession in oldestSessions)
                {
                    await _authSessionRepository.RevokeAsync(tenantId, oldSession.Id, now, cancellationToken);
                    await _refreshTokenRepository.RevokeBySessionAsync(tenantId, oldSession.Id, now, cancellationToken);
                }
            }
        }

        var sessionId = _idGeneratorAccessor.NextId();
        var sessionExpiresAt = now.AddMinutes(_jwtOptions.SessionExpiresMinutes);
        var clientContext = context.ClientContext;
        var session = new AuthSession(
            tenantId,
            account.Id,
            clientContext.ClientType.ToString(),
            clientContext.ClientPlatform.ToString(),
            clientContext.ClientChannel.ToString(),
            clientContext.ClientAgent.ToString(),
            context.IpAddress,
            context.UserAgent,
            now,
            sessionExpiresAt,
            sessionId);
        await _authSessionRepository.AddAsync(session, cancellationToken);

        var (refreshToken, refreshExpiresAt, refreshEntity) = CreateRefreshToken(account.Id, tenantId, sessionId, now, request.RememberMe);
        await _refreshTokenRepository.AddAsync(refreshEntity, cancellationToken);

        var appId = _appContextAccessor.GetAppId();
        var accessTokenResult = CreateAccessToken(account, tenantId, clientContext, sessionId, now, cancellationToken, appId);
        var accessToken = await accessTokenResult;
        return new AuthTokenResult(accessToken.Token, accessToken.ExpiresAt, refreshToken, refreshExpiresAt, sessionId);
    }

    public async Task<AuthTokenResult> RefreshTokenAsync(
        AuthRefreshRequest request,
        TenantId tenantId,
        AuthRequestContext context,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var tokenHash = ComputeTokenHash(request.RefreshToken);
        var storedToken = await _refreshTokenRepository.FindByHashAsync(tenantId, tokenHash, cancellationToken);
        if (storedToken is null)
        {
            await WriteAuditAsync(tenantId, "UNKNOWN", "TOKEN_REFRESH", "FAILED", null, context, cancellationToken);
            throw new BusinessException("刷新令牌无效或已过期", ErrorCodes.Unauthorized);
        }

        if (storedToken.RevokedAt.HasValue)
        {
            await HandleTokenReuseAsync(tenantId, storedToken, context, cancellationToken);
            throw new BusinessException("刷新令牌已失效", ErrorCodes.Unauthorized);
        }

        if (storedToken.ExpiresAt <= now)
        {
            await WriteAuditAsync(tenantId, storedToken.UserId.ToString(), "TOKEN_REFRESH", "EXPIRED", null, context, cancellationToken);
            throw new BusinessException("刷新令牌已过期", ErrorCodes.TokenExpired);
        }

        var session = await _authSessionRepository.FindByIdAsync(tenantId, storedToken.SessionId, cancellationToken);
        if (session is null || session.RevokedAt.HasValue || session.ExpiresAt <= now)
        {
            await _refreshTokenRepository.RevokeBySessionAsync(tenantId, storedToken.SessionId, now, cancellationToken);
            await WriteAuditAsync(tenantId, storedToken.UserId.ToString(), "TOKEN_REFRESH", "SESSION_INVALID", null, context, cancellationToken);
            throw new BusinessException("会话已失效", ErrorCodes.Unauthorized);
        }

        var account = await _userAccountRepository.FindByIdAsync(tenantId, storedToken.UserId, cancellationToken);
        if (account is null)
        {
            await WriteAuditAsync(tenantId, storedToken.UserId.ToString(), "TOKEN_REFRESH", "FAILED", null, context, cancellationToken);
            throw new BusinessException("账号不存在或已失效", ErrorCodes.Unauthorized);
        }

        if (!account.IsActive)
        {
            await WriteAuditAsync(tenantId, account.Username, "TOKEN_REFRESH", "FAILED", null, context, cancellationToken);
            throw new BusinessException("账号已停用", ErrorCodes.Forbidden);
        }

        var locked = IsLocked(account, now, out var lockStateChanged);
        if (lockStateChanged)
        {
            await _userAccountRepository.UpdateAsync(account, cancellationToken);
        }

        if (locked)
        {
            await WriteAuditAsync(tenantId, account.Username, "TOKEN_REFRESH", "LOCKED", null, context, cancellationToken);
            throw new BusinessException("账号已锁定", ErrorCodes.AccountLocked);
        }

        var passwordExpiredAt = account.LastPasswordChangeAt.AddDays(_passwordPolicy.ExpirationDays);
        if (passwordExpiredAt <= now)
        {
            await WriteAuditAsync(tenantId, account.Username, "TOKEN_REFRESH", "PASSWORD_EXPIRED", null, context, cancellationToken);
            throw new BusinessException("密码已过期", ErrorCodes.PasswordExpired);
        }

        if (storedToken.IssuedAt < account.LastPasswordChangeAt)
        {
            await _authSessionRepository.RevokeByUserIdAsync(tenantId, account.Id, now, cancellationToken);
            await _refreshTokenRepository.RevokeByUserIdAsync(tenantId, account.Id, now, cancellationToken);
            await WriteAuditAsync(tenantId, account.Username, "TOKEN_REFRESH", "PASSWORD_CHANGED", null, context, cancellationToken);
            throw new BusinessException("密码已变更，请重新登录", ErrorCodes.Unauthorized);
        }

        session.MarkSeen(now);
        await _authSessionRepository.UpdateAsync(session, cancellationToken);

        var (refreshToken, refreshExpiresAt, refreshEntity) = CreateRefreshToken(account.Id, tenantId, session.Id, now);
        storedToken.Revoke(now, refreshEntity.Id);
        await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);
        await _refreshTokenRepository.AddAsync(refreshEntity, cancellationToken);

        var appId = _appContextAccessor.GetAppId();
        var accessTokenResult = CreateAccessToken(account, tenantId, context.ClientContext, session.Id, now, cancellationToken, appId);
        var accessToken = await accessTokenResult;
        await WriteAuditAsync(tenantId, account.Username, "TOKEN_REFRESH", "SUCCESS", null, context, cancellationToken);
        return new AuthTokenResult(accessToken.Token, accessToken.ExpiresAt, refreshToken, refreshExpiresAt, session.Id);
    }

    public async Task RevokeSessionAsync(
        long userId,
        TenantId tenantId,
        long sessionId,
        AuthRequestContext context,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        await _authSessionRepository.RevokeAsync(tenantId, sessionId, now, cancellationToken);
        await _refreshTokenRepository.RevokeBySessionAsync(tenantId, sessionId, now, cancellationToken);
        // 退出登录后立即清除该 session 的认证缓存
        _authCacheService.InvalidateSession(tenantId, sessionId);
        await WriteAuditAsync(tenantId, userId.ToString(), "TOKEN_REVOKE", "SUCCESS", null, context, cancellationToken);
    }

    private static List<Claim> BuildClaims(
        UserAccount account,
        TenantId tenantId,
        IReadOnlyList<string> roleCodes,
        ClientContext clientContext,
        long sessionId,
        string appId)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Username),
            new("tenant_id", tenantId.Value.ToString("D")),
            new("sid", sessionId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Username),
            new("display_name", account.DisplayName),
            new("is_platform_admin", account.IsPlatformAdmin ? "true" : "false"),
            new("app_id", appId),
            new("client_type", clientContext.ClientType.ToString()),
            new("client_platform", clientContext.ClientPlatform.ToString()),
            new("client_channel", clientContext.ClientChannel.ToString()),
            new("client_agent", clientContext.ClientAgent.ToString())
        };

        if (roleCodes.Count > 0)
        {
            claims.AddRange(roleCodes.Select(role => new Claim(ClaimTypes.Role, role)));
        }

        return claims;
    }

    private async Task<(string Token, DateTimeOffset ExpiresAt)> CreateAccessToken(
        UserAccount account,
        TenantId tenantId,
        ClientContext clientContext,
        long sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        string appId)
    {
        var expires = now.AddMinutes(_jwtOptions.ExpiresMinutes);
        var roleCodes = await _rbacResolver.GetRoleCodesAsync(account, tenantId, cancellationToken);
        var claims = BuildClaims(account, tenantId, roleCodes, clientContext, sessionId, appId);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _jwtOptions.Issuer,
            _jwtOptions.Audience,
            claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var handler = new JwtSecurityTokenHandler();
        var tokenString = handler.WriteToken(token);
        return (tokenString, expires);
    }

    private (string Token, DateTimeOffset ExpiresAt, RefreshToken Entity) CreateRefreshToken(
        long userId,
        TenantId tenantId,
        long sessionId,
        DateTimeOffset now,
        bool rememberMe = false)
    {
        var expiryMinutes = rememberMe
            ? _jwtOptions.RememberMeRefreshExpiresMinutes
            : _jwtOptions.RefreshExpiresMinutes;
        var refreshExpiresAt = now.AddMinutes(expiryMinutes);
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var token = Base64UrlEncoder.Encode(tokenBytes);
        var hash = ComputeTokenHash(token);
        var entity = new RefreshToken(
            tenantId,
            userId,
            sessionId,
            hash,
            now,
            refreshExpiresAt,
            _idGeneratorAccessor.NextId());
        return (token, refreshExpiresAt, entity);
    }

    private static string ComputeTokenHash(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private async Task HandleTokenReuseAsync(
        TenantId tenantId,
        RefreshToken storedToken,
        AuthRequestContext context,
        CancellationToken cancellationToken)
    {
        if (storedToken.ReplacedById.HasValue)
        {
            var now = _timeProvider.GetUtcNow();
            await _authSessionRepository.RevokeAsync(tenantId, storedToken.SessionId, now, cancellationToken);
            await _refreshTokenRepository.RevokeBySessionAsync(tenantId, storedToken.SessionId, now, cancellationToken);
            await WriteAuditAsync(tenantId, storedToken.UserId.ToString(), "TOKEN_REFRESH", "REUSE_DETECTED", null, context, cancellationToken);
            return;
        }

        await WriteAuditAsync(tenantId, storedToken.UserId.ToString(), "TOKEN_REFRESH", "REVOKED", null, context, cancellationToken);
    }

    private bool IsLocked(UserAccount account, DateTimeOffset now, out bool stateChanged)
    {
        stateChanged = false;
        if (account.IsManualLocked && account.ManualLockAt > DateTimeOffset.MinValue)
        {
            var autoUnlockAt = account.ManualLockAt.AddMinutes(_lockoutPolicy.AutoUnlockMinutes);
            if (now >= autoUnlockAt)
            {
                account.Unlock();
                stateChanged = true;
                return false;
            }

            return true;
        }

        if (account.LockoutEndAt > DateTimeOffset.MinValue)
        {
            if (now >= account.LockoutEndAt)
            {
                account.Unlock();
                stateChanged = true;
                return false;
            }

            return true;
        }

        return false;
    }

    private Task WriteAuditAsync(
        TenantId tenantId,
        string actor,
        string action,
        string result,
        string? target,
        AuthRequestContext context,
        CancellationToken cancellationToken)
    {
        if (tenantId.IsEmpty)
        {
            // Skip audit if tenant ID is empty
            return Task.CompletedTask;
        }

        var auditContext = new AuditContext(
            tenantId,
            actor,
            action,
            result,
            target,
            context.IpAddress,
            context.UserAgent,
            context.ClientContext);

        return _auditRecorder.RecordAsync(auditContext, cancellationToken);
    }

    private Task WriteLoginLogAsync(
        TenantId tenantId,
        string username,
        AuthRequestContext context,
        bool success,
        string? message,
        DateTimeOffset loginTime,
        CancellationToken cancellationToken)
    {
        var request = new LoginLogWriteRequest(
            username,
            context.IpAddress ?? string.Empty,
            context.UserAgent,
            success,
            message,
            loginTime);
        return _loginLogWriteService.WriteAsync(tenantId, request, cancellationToken);
    }
}

