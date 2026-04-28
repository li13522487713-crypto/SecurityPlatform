using System.Security.Cryptography;

namespace Atlas.Connectors.Core.Security;

/// <summary>
/// OAuth state 票据：随登录跳转出，回调时核对，防 CSRF。
/// </summary>
public sealed record OAuthState
{
    public required string Value { get; init; }

    public required Guid TenantId { get; init; }

    public required long ProviderInstanceId { get; init; }

    public required string ProviderType { get; init; }

    public required string RedirectUri { get; init; }

    /// <summary>用户登录后落到本地的最终目的地（前端 URL，默认进入工作空间首页）。</summary>
    public string? PostLoginRedirect { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; init; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    public static string CreateValue(int byteLength = 32)
    {
        Span<byte> buf = stackalloc byte[byteLength];
        RandomNumberGenerator.Fill(buf);
        return Convert.ToBase64String(buf).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

/// <summary>
/// OAuth state 票据存储：默认进程内实现；可被 Infrastructure.ExternalConnectors 中的分布式实现替换。
/// </summary>
public interface IOAuthStateStore
{
    Task SaveAsync(OAuthState state, CancellationToken cancellationToken);

    /// <summary>
    /// 取出并立即从存储删除（单次使用语义），保证一个 state 不能被回放。
    /// </summary>
    Task<OAuthState?> ConsumeAsync(string stateValue, CancellationToken cancellationToken);
}
