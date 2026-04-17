using System.Collections.Concurrent;
using System.Security.Cryptography;
using Atlas.Application.Abstractions;
using Atlas.Application.Options;
using Atlas.Application.SetupConsole.Abstractions;
using Atlas.Application.SetupConsole.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Setup.Entities;
using Microsoft.Extensions.Options;
using SqlSugar;

namespace Atlas.Infrastructure.Services.SetupConsole;

/// <summary>
/// 控制台二次认证服务（M5）。
///
/// - ConsoleToken 30 分钟过期
/// - 校验通过：恢复密钥 hash 匹配 <c>SystemSetupState.RecoveryKeyHash</c>，或 BootstrapAdmin 凭证匹配
/// - 内存 ConcurrentDictionary 维护活跃 token；进程重启 token 全部失效（M7 可升级到 Redis / DB 持久化）
/// </summary>
public sealed class SetupRecoveryKeyService : ISetupRecoveryKeyService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    private static readonly ConcurrentDictionary<string, ConsoleAuthTokenDto> ActiveTokens = new(StringComparer.Ordinal);

    private readonly ISqlSugarClient _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOptions<BootstrapAdminOptions> _bootstrapAdmin;

    public SetupRecoveryKeyService(
        ISqlSugarClient db,
        ITenantProvider tenantProvider,
        IPasswordHasher passwordHasher,
        IOptions<BootstrapAdminOptions> bootstrapAdmin)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _passwordHasher = passwordHasher;
        _bootstrapAdmin = bootstrapAdmin;
    }

    public async Task<ConsoleAuthTokenDto?> AuthenticateAsync(
        ConsoleAuthChallengeRequest request,
        CancellationToken cancellationToken = default)
    {
        var ok = await TryRecoveryKeyAsync(request.RecoveryKey, cancellationToken)
                 || TryBootstrapAdminCredentials(request.BootstrapAdminUsername, request.BootstrapAdminPassword);

        if (!ok)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var token = new ConsoleAuthTokenDto(
            ConsoleToken: GenerateConsoleToken(),
            IssuedAt: now,
            ExpiresAt: now.Add(TokenLifetime),
            Permissions: new[] { "system", "workspace", "migration" });
        ActiveTokens[token.ConsoleToken] = token;
        return token;
    }

    public Task<ConsoleAuthTokenDto?> RefreshAsync(string consoleToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consoleToken))
        {
            return Task.FromResult<ConsoleAuthTokenDto?>(null);
        }

        if (!ActiveTokens.TryGetValue(consoleToken, out var existing))
        {
            return Task.FromResult<ConsoleAuthTokenDto?>(null);
        }

        var now = DateTimeOffset.UtcNow;
        if (existing.ExpiresAt <= now)
        {
            ActiveTokens.TryRemove(consoleToken, out _);
            return Task.FromResult<ConsoleAuthTokenDto?>(null);
        }

        var renewed = existing with
        {
            IssuedAt = now,
            ExpiresAt = now.Add(TokenLifetime)
        };
        ActiveTokens[consoleToken] = renewed;
        return Task.FromResult<ConsoleAuthTokenDto?>(renewed);
    }

    public Task<bool> ValidateAsync(string consoleToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consoleToken))
        {
            return Task.FromResult(false);
        }

        if (!ActiveTokens.TryGetValue(consoleToken, out var existing))
        {
            return Task.FromResult(false);
        }

        if (existing.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            ActiveTokens.TryRemove(consoleToken, out _);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task RevokeAsync(string consoleToken, CancellationToken cancellationToken = default)
    {
        ActiveTokens.TryRemove(consoleToken, out _);
        return Task.CompletedTask;
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
            // 状态机记录由 SetupConsoleService 维护；这里只在已存在的行上写 hash。
            // 若调用方未先创建 SystemSetupState 行，则无法写入；不抛异常以避免阻断 BootstrapUser 步骤。
            return plain;
        }

        existing.SetRecoveryKeyHash(hash, DateTimeOffset.UtcNow);
        await _db.Updateable(existing).ExecuteCommandAsync().ConfigureAwait(false);
        return plain;
    }

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

    private bool TryBootstrapAdminCredentials(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var options = _bootstrapAdmin.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Password))
        {
            return false;
        }

        return string.Equals(username.Trim(), options.Username, StringComparison.Ordinal)
               && string.Equals(password, options.Password, StringComparison.Ordinal);
    }

    private static string GenerateConsoleToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return $"console-{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private static string GenerateRecoveryKeyPlainText()
    {
        // ATLS- + 5 段 4 字符 base32（去掉易混 0/O/1/I）
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var segments = new string[5];
        Span<char> buffer = stackalloc char[4];
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
}
