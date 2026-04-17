using System.Security.Cryptography;
using Atlas.Application.Abstractions;
using Atlas.Application.Options;
using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.Setup.Entities;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services.SetupConsole;

/// <summary>
/// 控制台二次认证服务（M5 内存版 → M8 持久化版）。
///
/// - ConsoleToken 30 分钟过期；持久化到 <c>setup_console_token</c> 表。
/// - 校验通过：恢复密钥 hash 匹配 <see cref="SystemSetupState.RecoveryKeyHash"/>，
///   或 BootstrapAdmin 凭证（密码用 PBKDF2 哈希后存到 <see cref="SystemSetupState.BootstrapPasswordHash"/>，
///   PlatformHost 启动时由 <c>SetupConsoleBootstrapInitializer</c> 自动哈希；运行时 timing-safe 比对）。
/// - 仅存哈希；明文 token 仅在颁发时返回客户端一次。
/// - 多实例部署时所有实例共享 token 生命周期。
/// </summary>
public sealed class SetupRecoveryKeyService : ISetupRecoveryKeyService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    private readonly ISqlSugarClient _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGen;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOptions<BootstrapAdminOptions> _bootstrapAdmin;

    public SetupRecoveryKeyService(
        ISqlSugarClient db,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGen,
        IPasswordHasher passwordHasher,
        IOptions<BootstrapAdminOptions> bootstrapAdmin)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _idGen = idGen;
        _passwordHasher = passwordHasher;
        _bootstrapAdmin = bootstrapAdmin;
    }

    public async Task<ConsoleAuthTokenDto?> AuthenticateAsync(
        ConsoleAuthChallengeRequest request,
        CancellationToken cancellationToken = default)
    {
        var ok = await TryRecoveryKeyAsync(request.RecoveryKey, cancellationToken).ConfigureAwait(false)
                 || await TryBootstrapAdminCredentialsAsync(
                        request.BootstrapAdminUsername, request.BootstrapAdminPassword, cancellationToken)
                    .ConfigureAwait(false);

        if (!ok)
        {
            return null;
        }

        return await IssueTokenAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ConsoleAuthTokenDto?> RefreshAsync(
        string consoleToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consoleToken))
        {
            return null;
        }

        var hash = HashToken(consoleToken);
        var tenant = _tenantProvider.GetTenantId().Value;
        var record = await _db.Queryable<SetupConsoleToken>()
            .Where(item => item.TenantIdValue == tenant && item.TokenHash == hash)
            .FirstAsync()
            .ConfigureAwait(false);
        if (record is null || !record.IsActive(DateTimeOffset.UtcNow))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        record.Renew(now, now.Add(TokenLifetime));
        await _db.Updateable(record).ExecuteCommandAsync().ConfigureAwait(false);

        return new ConsoleAuthTokenDto(
            ConsoleToken: consoleToken,
            IssuedAt: record.IssuedAt,
            ExpiresAt: record.ExpiresAt,
            Permissions: SplitPermissions(record.Permissions));
    }

    public async Task<bool> ValidateAsync(string consoleToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consoleToken))
        {
            return false;
        }

        var hash = HashToken(consoleToken);
        var tenant = _tenantProvider.GetTenantId().Value;
        var record = await _db.Queryable<SetupConsoleToken>()
            .Where(item => item.TenantIdValue == tenant && item.TokenHash == hash)
            .FirstAsync()
            .ConfigureAwait(false);
        return record is not null && record.IsActive(DateTimeOffset.UtcNow);
    }

    public async Task RevokeAsync(string consoleToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consoleToken))
        {
            return;
        }
        var hash = HashToken(consoleToken);
        var tenant = _tenantProvider.GetTenantId().Value;
        var record = await _db.Queryable<SetupConsoleToken>()
            .Where(item => item.TenantIdValue == tenant && item.TokenHash == hash)
            .FirstAsync()
            .ConfigureAwait(false);
        if (record is null)
        {
            return;
        }
        record.Revoke(DateTimeOffset.UtcNow);
        await _db.Updateable(record).ExecuteCommandAsync().ConfigureAwait(false);
    }

    public async Task<string> GenerateAndPersistRecoveryKeyAsync(CancellationToken cancellationToken = default)
    {
        var plain = GenerateRecoveryKeyPlainText();
        var hash = _passwordHasher.HashPassword(plain);
        var tenantId = _tenantProvider.GetTenantId();

        var existing = await _db
            .Queryable<SystemSetupState>()
            .Where(item => item.TenantIdValue == tenantId.Value)
            .FirstAsync()
            .ConfigureAwait(false);

        if (existing is null)
        {
            // 首次生成时若 SystemSetupState 不存在则创建（与 SetupConsoleService.EnsureSystemStateAsync 同步逻辑）
            existing = new SystemSetupState(tenantId, _idGen.NextId(), "v1", DateTimeOffset.UtcNow);
            existing.SetRecoveryKeyHash(hash, DateTimeOffset.UtcNow);
            await _db.Insertable(existing).ExecuteCommandAsync().ConfigureAwait(false);
            return plain;
        }

        existing.SetRecoveryKeyHash(hash, DateTimeOffset.UtcNow);
        await _db.Updateable(existing).ExecuteCommandAsync().ConfigureAwait(false);
        return plain;
    }

    /// <summary>
    /// 由 <c>SetupConsoleBootstrapInitializer</c> 启动时调用：把 BootstrapAdmin 明文密码 PBKDF2 后落库。
    /// 已存在 hash 时跳过；密码变化时由调用方显式传 <c>force=true</c>。
    /// </summary>
    public async Task EnsureBootstrapPasswordHashAsync(string plainPassword, bool force, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(plainPassword))
        {
            return;
        }

        var tenantId = _tenantProvider.GetTenantId();
        var existing = await _db
            .Queryable<SystemSetupState>()
            .Where(item => item.TenantIdValue == tenantId.Value)
            .FirstAsync()
            .ConfigureAwait(false);
        if (existing is null)
        {
            existing = new SystemSetupState(tenantId, _idGen.NextId(), "v1", DateTimeOffset.UtcNow);
            existing.SetBootstrapPasswordHash(_passwordHasher.HashPassword(plainPassword), DateTimeOffset.UtcNow);
            await _db.Insertable(existing).ExecuteCommandAsync().ConfigureAwait(false);
            return;
        }

        if (!force && !string.IsNullOrEmpty(existing.BootstrapPasswordHash))
        {
            // 已有 hash 且未强制刷新，验证一下是否还匹配；不匹配说明配置已轮换，重新哈希
            if (_passwordHasher.VerifyHashedPassword(existing.BootstrapPasswordHash, plainPassword))
            {
                return;
            }
        }

        existing.SetBootstrapPasswordHash(_passwordHasher.HashPassword(plainPassword), DateTimeOffset.UtcNow);
        await _db.Updateable(existing).ExecuteCommandAsync().ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- internals

    private async Task<bool> TryRecoveryKeyAsync(string? recoveryKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recoveryKey))
        {
            return false;
        }

        var tenantId = _tenantProvider.GetTenantId();
        var state = await _db
            .Queryable<SystemSetupState>()
            .Where(item => item.TenantIdValue == tenantId.Value)
            .FirstAsync()
            .ConfigureAwait(false);
        if (state is null || string.IsNullOrEmpty(state.RecoveryKeyHash))
        {
            return false;
        }

        return _passwordHasher.VerifyHashedPassword(state.RecoveryKeyHash, recoveryKey.Trim());
    }

    private async Task<bool> TryBootstrapAdminCredentialsAsync(string? username, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var options = _bootstrapAdmin.Value;
        if (!options.Enabled)
        {
            return false;
        }

        if (!string.Equals(username.Trim(), options.Username, StringComparison.Ordinal))
        {
            return false;
        }

        var tenantId = _tenantProvider.GetTenantId();
        var state = await _db
            .Queryable<SystemSetupState>()
            .Where(item => item.TenantIdValue == tenantId.Value)
            .FirstAsync()
            .ConfigureAwait(false);

        // 优先使用 DB 中的 BootstrapPasswordHash；若不存在则降级到 timing-safe 明文比对（仅 dev 兜底）。
        if (state is not null && !string.IsNullOrEmpty(state.BootstrapPasswordHash))
        {
            return _passwordHasher.VerifyHashedPassword(state.BootstrapPasswordHash, password);
        }

        if (string.IsNullOrEmpty(options.Password))
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(password),
            System.Text.Encoding.UTF8.GetBytes(options.Password));
    }

    private async Task<ConsoleAuthTokenDto> IssueTokenAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var plain = GenerateConsoleToken();
        var hash = HashToken(plain);
        var permissions = "system,workspace,migration";
        var record = new SetupConsoleToken(
            _tenantProvider.GetTenantId(),
            _idGen.NextId(),
            hash,
            permissions,
            now,
            now.Add(TokenLifetime));
        await _db.Insertable(record).ExecuteCommandAsync().ConfigureAwait(false);

        return new ConsoleAuthTokenDto(
            ConsoleToken: plain,
            IssuedAt: now,
            ExpiresAt: record.ExpiresAt,
            Permissions: SplitPermissions(permissions));
    }

    private static string GenerateConsoleToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return $"console-{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private static string HashToken(string consoleToken)
    {
        // SHA256 单向哈希；避免 DB 泄漏后令牌可直接复用。Token 本身已是高熵随机串，
        // 不需要 PBKDF2/salt。
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(consoleToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GenerateRecoveryKeyPlainText()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> buffer = stackalloc char[4];
        var segments = new string[5];
        for (var seg = 0; seg < 5; seg += 1)
        {
            for (var i = 0; i < 4; i += 1)
            {
                buffer[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }
            segments[seg] = new string(buffer);
        }
        return $"ATLS-{string.Join('-', segments)}";
    }

    private static IReadOnlyList<string> SplitPermissions(string permissions)
    {
        if (string.IsNullOrWhiteSpace(permissions))
        {
            return Array.Empty<string>();
        }
        return permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
